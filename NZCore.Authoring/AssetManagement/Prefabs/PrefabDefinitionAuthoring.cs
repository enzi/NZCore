// <copyright project="NZCore.Authoring" file="PrefabDefinitionAuthoring.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
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

                DependsOn(authoring.Definition);
                DependsOn(authoring.Definition.Group);

                if (authoring.gameObject.scene.IsValid())
                {
                    BakeSpawner(authoring);
                    return;
                }

                if (!authoring.Definition.TryGetPrefabId(PrefabDefinition.DefaultKey, out var prefabId))
                {
                    Debug.LogError(
                        $"{authoring.Definition.name} has no ID for its default key, so {authoring.name} bakes no PrefabId. Save the definition to assign one.",
                        authoring);
                    return;
                }

                AddComponent(GetEntity(TransformUsageFlags.None), prefabId);
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

                if (authoring.Definition.Target is not GameObject prefab)
                {
                    Debug.LogError($"{authoring.Definition.name} has no prefab, so the placed instance {authoring.name} spawns nothing.", authoring);
                    return;
                }

                var transform = GetComponent<Transform>();
                var scale = transform.lossyScale;

                if (!Mathf.Approximately(scale.x, scale.y) || !Mathf.Approximately(scale.x, scale.z))
                {
                    Debug.LogWarning($"{authoring.name} has a non-uniform scale {scale}. Its spawned instance uses the uniform scale {scale.x}.", authoring);
                }

                var spawner = CreateAdditionalEntity(TransformUsageFlags.None, false, $"{authoring.name} Spawner");

                AddComponent(spawner, new PrefabInstanceSpawner
                {
                    Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic),
                    Transform = LocalTransform.FromPositionRotationScale(transform.position, transform.rotation, scale.x)
                });
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