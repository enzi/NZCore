// <copyright project="NZCore.Authoring" file="PrefabDefinitionAuthoring.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace NZCore.AssetManagement
{
    [DisallowMultipleComponent]
    public class PrefabDefinitionAuthoring : MonoBehaviour
    {
        public PrefabDefinition Definition;

        private class PrefabDefinitionBaker : Baker<PrefabDefinitionAuthoring>
        {
            public override void Bake(PrefabDefinitionAuthoring authoring)
            {
                if (authoring.Definition == null)
                {
                    Debug.LogError($"{authoring.name} has a PrefabDefinitionAuthoring without a PrefabDefinition.", authoring);
                    return;
                }

                if (authoring.gameObject.scene.IsValid())
                {
                    DependsOn(authoring.Definition);
                    BakeSpawner(authoring);
                    return;
                }

                var prefabId = PrefabIdBaking.FromDefinition(this, authoring.Definition);

                if (!prefabId.IsNull)
                {
                    AddComponent(GetEntity(TransformUsageFlags.None), prefabId);
                }
            }

            private void BakeSpawner(PrefabDefinitionAuthoring authoring)
            {
                var self = GetEntity(TransformUsageFlags.None);
                AddComponent<BakingOnlyEntity>(self);

                var children = AddBuffer<BakingOnlyChild>(self);

                foreach (var child in GetChildren(true))
                {
                    children.Add(new BakingOnlyChild { Entity = GetEntity(child, TransformUsageFlags.None) });
                }

                var parent = GetParent();

                if (!IsActive() || (parent != null && GetComponentInParent<PrefabDefinitionAuthoring>(parent) != null))
                {
                    return;
                }

                Debug.LogWarning($"{authoring.name} is a prefab placed directly in a subscene. It still spawns, but overrides on it are lost. " +
                                 "Convert it with Tools/NZCore/Convert Placed Prefabs to Spawners.", authoring);

                if (!PrefabSpawnerAuthoring.TryCreateSpawner(this, authoring.Definition, authoring, out var spawnerData))
                {
                    return;
                }

                var spawner = CreateAdditionalEntity(TransformUsageFlags.None, false, $"{authoring.name} Spawner");
                AddComponent(spawner, spawnerData);
            }
        }
    }

    [TemporaryBakingType]
    internal struct BakingOnlyChild : IBufferElementData
    {
        public Entity Entity;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    internal partial class BakingOnlyChildSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);

            foreach (var children in SystemAPI.Query<DynamicBuffer<BakingOnlyChild>>().WithOptions(EntityQueryOptions.IncludeDisabledEntities))
            {
                foreach (var child in children)
                {
                    commandBuffer.AddComponent<BakingOnlyEntity>(child.Entity);
                }
            }

            commandBuffer.Playback(EntityManager);
        }
    }
}