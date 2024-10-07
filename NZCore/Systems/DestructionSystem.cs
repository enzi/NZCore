// <copyright project="NZCore" file="DestructionSystem.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(NZDestroySystemGroup), OrderFirst = true)]
    public partial struct PreDestructionSystem : ISystem
    {
        private EntityQuery query;

        private DestructionMap destructionMap;

        private Entity destructionMapEntity;
        private NativeList<Entity> keysToRemove;

        public void OnCreate(ref SystemState state)
        {
            keysToRemove = new NativeList<Entity>(0, Allocator.Persistent);
            destructionMapEntity = state.EntityManager.CreateEntity();

            destructionMap = new DestructionMap();
            destructionMap.Allocate();

            state.EntityManager.AddComponentData(destructionMapEntity, destructionMap);
            state.EntityManager.AddBuffer<DeferredDestroyMapBuffer>(destructionMapEntity);

            query = SystemAPI.QueryBuilder()
                .WithAll<DestroyEntity>()
                .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
            destructionMap.Dispose();
            keysToRemove.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deferredBuffer = SystemAPI.GetBuffer<DeferredDestroyMapBuffer>(destructionMapEntity);

            if (deferredBuffer.Length > 0)
            {
                var deferredArray = deferredBuffer.AsNativeArray();

                foreach (var deferredElement in deferredArray)
                {
                    destructionMap.Add(deferredElement.Parent, deferredElement.Child, deferredElement.DestroyMethod, deferredElement.Payload);
                }

                deferredBuffer.Clear();
            }

            if (!query.IsEmpty)
            {
                var entities = query.ToEntityArray(Allocator.Temp);

                foreach (var entity in entities)
                {
                    if (!destructionMap.Map.TryGetValue(entity, out var list))
                        continue;

                    foreach (var cleanup in list)
                    {
                        switch (cleanup.DestroyMethod)
                        {
                            case ChildDestroyMethod.Destroy:
                            {
                                //Debug.Log($"Queuing entity {cleanup.Entity} for destroy");
                                state.EntityManager.SetComponentEnabled<DestroyEntity>(cleanup.Entity, true);
                                break;
                            }
                            case ChildDestroyMethod.Cleanup:
                            {
                                //Debug.Log($"Queuing entity {cleanup.Entity} for cleanup");
                                state.EntityManager.SetComponentEnabled<NZCleanupEntity>(cleanup.Entity, true);
                                break;
                            }
                            case ChildDestroyMethod.None:
                            default:
                                break;
                        }
                    }

                    keysToRemove.Add(entity);
                }
            }
        }
    }

    [UpdateInGroup(typeof(NZDestroySystemGroup), OrderLast = true)]
    [CreateAfter(typeof(PreDestructionSystem))]
    public partial struct DestructionSystem : ISystem
    {
        private EntityQuery destroyQuery;
        private EntityQuery cleanupQuery;

        public void OnCreate(ref SystemState state)
        {
            destroyQuery = SystemAPI.QueryBuilder()
                .WithAll<DestroyEntity>()
                .Build();

            cleanupQuery = SystemAPI.QueryBuilder()
                .WithAll<NZCleanupEntity>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!cleanupQuery.IsEmpty)
            {
                //Debug.Log("Running cleanupQuery");
                state.EntityManager.SetComponentEnabled<NZCleanupEntity>(cleanupQuery, false);
            }

            if (!destroyQuery.IsEmpty)
            {
                var destructionMap = SystemAPI.GetSingleton<DestructionMap>();

                var entities = destroyQuery.ToEntityArray(Allocator.Temp);

                foreach (var entity in entities)
                {
                    //Debug.Log($"Destroying {entity}");
                    state.EntityManager.DestroyEntity(entity);

                    if (!destructionMap.Map.TryGetValue(entity, out var list))
                        continue;

                    list.Dispose();
                    destructionMap.Map.Remove(entity);
                }

                //Debug.Log("Running destroyQuery");

                // todo, this throws the error, which is actually wrong -.-
                // ArgumentException: DestroyEntity(EntityQuery query) is destroying entity Entity(1089:1) 'BaseMonsterPrefab' which
                // contains a LinkedEntityGroup and the entity Entity(1089:1) 'BaseMonsterPrefab' in that group is not included in the query. 
                //state.EntityManager.DestroyEntity(destroyQuery);
            }
        }
    }
}