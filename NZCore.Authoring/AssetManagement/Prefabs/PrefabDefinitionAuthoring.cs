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

                var parent = GetParent();

                if (parent != null && (!authoring.gameObject.scene.IsValid() || GetComponentInParent<PrefabDefinitionAuthoring>(parent) != null))
                {
                    Debug.LogError($"{authoring.name} has a PrefabDefinitionAuthoring but isn't the root of its prefab. " +
                                   $"Only a prefab's root can have a PrefabDefinition, so {authoring.name} bakes no PrefabId.", authoring);
                    return;
                }

                if (authoring.gameObject.scene.IsValid())
                {
                    BakePlacedInstance(authoring);
                    return;
                }

                var prefabId = PrefabIdBaking.FromDefinition(this, authoring.Definition);

                if (!prefabId.IsNull)
                {
                    AddComponent(GetEntity(TransformUsageFlags.None), prefabId);
                }
            }

            private void BakePlacedInstance(PrefabDefinitionAuthoring authoring)
            {
                var self = GetEntity(TransformUsageFlags.None);
                AddComponent<BakingOnlyEntity>(self);

                var children = AddBuffer<BakingOnlyChild>(self);

                foreach (var child in GetChildren(true))
                {
                    children.Add(new BakingOnlyChild { Entity = GetEntity(child, TransformUsageFlags.None) });
                }

                if (!IsActive())
                {
                    return;
                }

                Debug.LogError($"{authoring.name} is a prefab placed directly in a subscene, so it bakes nothing. " +
                               "Convert it to a spawner with Tools/NZCore/Convert Placed Prefabs to Spawners.", authoring);
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