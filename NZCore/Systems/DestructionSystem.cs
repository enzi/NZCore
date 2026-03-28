// <copyright project="NZCore" file="DestructionSystem.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(NZDestroySystemGroup), OrderFirst = true)]
    public partial struct PreDestructionSystem : ISystem
    {
        private EntityQuery _query;

        private DestructionMap _destructionMap;

        private Entity _destructionMapEntity;
        private NativeList<Entity> _keysToRemove;

        public void OnCreate(ref SystemState state)
        {
            _keysToRemove = new NativeList<Entity>(0, Allocator.Persistent);
            _destructionMapEntity = state.EntityManager.CreateEntity();

            _destructionMap = new DestructionMap();
            _destructionMap.Allocate();

            state.EntityManager.AddComponentData(_destructionMapEntity, _destructionMap);
            state.EntityManager.AddBuffer<DeferredDestroyMapBuffer>(_destructionMapEntity);

            _query = SystemAPI.QueryBuilder()
                              .WithAll<DestroyEntity>()
                              .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
            _destructionMap.Dispose();
            _keysToRemove.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deferredBuffer = SystemAPI.GetBuffer<DeferredDestroyMapBuffer>(_destructionMapEntity);

            if (deferredBuffer.Length > 0)
            {
                var deferredArray = deferredBuffer.AsNativeArray();

                foreach (var deferredElement in deferredArray)
                {
                    _destructionMap.Add(deferredElement.Parent, deferredElement.Child, deferredElement.DestroyMethod, deferredElement.Payload);
                }

                deferredBuffer.Clear();
            }

            if (!_query.IsEmpty)
            {
                // maybe jobify this one day and test if main thread is faster than parallel scheduling a job 
                var entities = _query.ToEntityArray(Allocator.Temp);

                foreach (var entity in entities)
                {
                    if (!_destructionMap.Map.TryGetValue(entity, out var list))
                    {
                        continue;
                    }

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

                    _keysToRemove.Add(entity);
                }
            }
        }
    }

    [UpdateInGroup(typeof(NZDestroySystemGroup), OrderLast = true)]
    [CreateAfter(typeof(PreDestructionSystem))]
    public partial struct DestructionSystem : ISystem
    {
        private EntityQuery _destroyQuery;
        private EntityQuery _cleanupQuery;

        public void OnCreate(ref SystemState state)
        {
            _destroyQuery = SystemAPI.QueryBuilder()
                                     .WithAll<DestroyEntity>()
                                     .Build();

            _cleanupQuery = SystemAPI.QueryBuilder()
                                     .WithAll<NZCleanupEntity>()
                                     .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!_cleanupQuery.IsEmpty)
            {
                //Debug.Log("Running cleanupQuery");
                state.EntityManager.SetComponentEnabled<NZCleanupEntity>(_cleanupQuery, false);
            }

            if (!_destroyQuery.IsEmpty)
            {
                var destructionMap = SystemAPI.GetSingleton<DestructionMap>();

                var entities = _destroyQuery.ToEntityArray(Allocator.Temp);

                foreach (var entity in entities)
                {
                    //Debug.Log($"Destroying {entity}");
                    state.EntityManager.DestroyEntity(entity);

                    if (!destructionMap.Map.TryGetValue(entity, out var list))
                    {
                        continue;
                    }

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