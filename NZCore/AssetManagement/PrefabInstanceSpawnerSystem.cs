// <copyright project="NZCore" file="PrefabInstanceSpawnerSystem.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace NZCore.AssetManagement
{
    [UpdateInGroup(typeof(InstantiateSystemGroup))]
    public partial struct PrefabInstanceSpawnerSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder().WithAll<PrefabInstanceSpawner>().Build();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var spawners = _query.ToComponentDataArray<PrefabInstanceSpawner>(Allocator.Temp);
            state.EntityManager.DestroyEntity(_query);

            foreach (var spawner in spawners)
            {
                var instance = state.EntityManager.Instantiate(spawner.Prefab);
                state.EntityManager.SetComponentData(instance, spawner.Transform);
            }
        }
    }
}