// <copyright project="NZCore.Authoring" file="PrefabSpawnerAuthoring.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace NZCore.AssetManagement
{
    [DisallowMultipleComponent]
    public class PrefabSpawnerAuthoring : MonoBehaviour
    {
        public PrefabDefinition Definition;

        internal static bool TryCreateSpawner(IBaker baker, PrefabDefinition definition, Object context, out PrefabInstanceSpawner spawner)
        {
            spawner = default;
            baker.DependsOn(definition);

            if (definition.Target is not GameObject prefab)
            {
                Debug.LogError($"{definition.name} has no prefab, so {context.name} spawns nothing.", context);
                return false;
            }

            var transform = baker.GetComponent<Transform>();
            var scale = transform.lossyScale;

            if (!Mathf.Approximately(scale.x, scale.y) || !Mathf.Approximately(scale.x, scale.z))
            {
                Debug.LogWarning($"{context.name} has a non-uniform scale {scale}. Its spawned instance uses the uniform scale {scale.x}.", context);
            }

            spawner = new PrefabInstanceSpawner
            {
                Prefab = baker.GetEntity(prefab, TransformUsageFlags.Dynamic),
                Transform = LocalTransform.FromPositionRotationScale(transform.position, transform.rotation, scale.x)
            };

            return true;
        }

        private class Baker : Baker<PrefabSpawnerAuthoring>
        {
            public override void Bake(PrefabSpawnerAuthoring authoring)
            {
                if (authoring.Definition == null)
                {
                    Debug.LogError($"{authoring.name} has a PrefabSpawnerAuthoring without a PrefabDefinition.", authoring);
                    return;
                }

                if (TryCreateSpawner(this, authoring.Definition, authoring, out var spawner))
                {
                    var entity = GetEntity(TransformUsageFlags.None);
                    spawner.Overrides = PrefabDefinitionOverrideAuthoring.Bake(this, GetComponent<PrefabDefinitionOverrideAuthoring>());
                    AddComponent(entity, spawner);
                    AddComponent<PrefabDefinitionSpawnRequest>(entity);
                }
            }
        }
    }
}