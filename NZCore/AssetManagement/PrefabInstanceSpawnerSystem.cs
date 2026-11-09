// <copyright project="NZCore" file="PrefabInstanceSpawnerSystem.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace NZCore.AssetManagement
{
    [UpdateInGroup(typeof(InstantiateSystemGroup))]
    public partial struct PrefabInstanceSpawnerSystem : ISystem
    {
        private EntityQuery _query;
        private NativeHashSet<TypeIndex> _defaultTypes;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder().WithAll<PrefabInstanceSpawner, PrefabDefinitionSpawnRequest>().Build();
            state.RequireForUpdate(_query);

            _defaultTypes = new NativeHashSet<TypeIndex>(10, Allocator.Persistent)
            {
                TypeManager.GetTypeIndex<Disabled>(),
                TypeManager.GetTypeIndex<Simulate>(),
                TypeManager.GetTypeIndex<EntityGuid>(),
                TypeManager.GetTypeIndex<SceneTag>(),
                TypeManager.GetTypeIndex<SceneSection>(),
                TypeManager.GetTypeIndex<Static>(),
                TypeManager.GetTypeIndex<LocalTransform>(),
                TypeManager.GetTypeIndex<LocalToWorld>(),
                TypeManager.GetTypeIndex<PostTransformMatrix>(),
                TypeManager.GetTypeIndex<Parent>()
            };
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _defaultTypes.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var spawners = _query.ToEntityArray(Allocator.Temp);

            foreach (var spawner in spawners)
            {
                var data = entityManager.GetComponentData<PrefabInstanceSpawner>(spawner);
                var instance = entityManager.Instantiate(data.Prefab);

                entityManager.SetComponentData(instance, data.Transform);

                if (data.Overrides != Entity.Null)
                {
                    PrefabDefinitionOverrides.Apply(entityManager, _defaultTypes, spawner, data.Overrides, instance);
                }
            }

            entityManager.SetComponentEnabled<PrefabDefinitionSpawnRequest>(_query, false);
        }
    }
}
