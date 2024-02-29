using NZSpellCasting;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace NZCore
{
    public enum ChildDestroyMethod : byte
    {
        Destroy,
        Cleanup
    }
    
    public struct DestroyEntity : IComponentData, IEnableableComponent
    {
    }
    
    public struct NZCleanupEntity : IComponentData, IEnableableComponent
    {
    }

    public struct DestroyMapElement
    {
        public Entity Entity;
        public ChildDestroyMethod DestroyMethod;
    }

    public struct DestructionMap : IComponentData
    {
        private NativeHashMap<Entity, UnsafeList<DestroyMapElement>> map;

        public void Init(NativeHashMap<Entity, UnsafeList<DestroyMapElement>> newMap)
        {
            map = newMap;
        }

        public unsafe void Add(ref SystemState state, Entity parent, Entity child, ChildDestroyMethod destroyMethod)
        {
            switch (destroyMethod)
            {
                case ChildDestroyMethod.Destroy:
                {
                    if (!state.EntityManager.HasComponent<DestroyEntity>(child))
                    {
                        state.EntityManager.AddComponent<DestroyEntity>(child);
                        state.EntityManager.SetComponentEnabled<DestroyEntity>(child, false);
                    }

                    break;
                }
                case ChildDestroyMethod.Cleanup:
                {
                    if (!state.EntityManager.HasComponent<NZCleanupEntity>(child))
                    {
                        state.EntityManager.AddComponent<NZCleanupEntity>(child);
                        state.EntityManager.SetComponentEnabled<NZCleanupEntity>(child, false);
                    }

                    break;
                }
            }
            
            //Debug.Log($"Adding {child} to {parent} - {destroyMethod}");

            if (map.TryGetRefValue(parent, out var listPtr))
            {
                listPtr->Add(new DestroyMapElement()
                {
                    Entity = child,
                    DestroyMethod = destroyMethod
                });
            }
            else
            {
                var newList = new UnsafeList<DestroyMapElement>(1, Allocator.Persistent);
                newList.Add(new DestroyMapElement()
                {
                    Entity = child,
                    DestroyMethod = destroyMethod
                });
                
                map.Add(parent, newList);
            }
        }

        public void AddLinkedEntityGroup(ref SystemState state, Entity parent)
        {
            var linkedEntityGroup = state.EntityManager.GetBuffer<LinkedEntityGroup>(parent);
            var copy = linkedEntityGroup.ToNativeArray(Allocator.Temp).Reinterpret<Entity>();

            foreach (var entity in copy)
            {
                if (entity == parent)
                    continue;
                
                Add(ref state, parent, entity, ChildDestroyMethod.Destroy);
            }
        }

        public unsafe void Remove(Entity parent, Entity child)
        {
            if (!map.TryGetRefValue(parent, out var listPtr)) 
                return;

            var startIndex = listPtr->Length - 1;
            for (int i = startIndex; i >= 0; i--)
            {
                if (listPtr->ElementAt(i).Entity == child)
                    listPtr->RemoveAtSwapBack(i);
            }
        }
    }
    
    [UpdateInGroup(typeof(NZDestroySystemGroup), OrderFirst = true)]
    public partial struct PreDestructionSystem : ISystem
    {
        private EntityQuery query;
        
        private NativeHashMap<Entity, UnsafeList<DestroyMapElement>> map;
        
        public void OnCreate(ref SystemState state)
        {
            map = new NativeHashMap<Entity, UnsafeList<DestroyMapElement>>(0, Allocator.Persistent);
            
            var entity = state.EntityManager.CreateEntity();

            var destructionMap = new DestructionMap();
            destructionMap.Init(map);
            
            state.EntityManager.AddComponentData(entity, destructionMap);

            query = SystemAPI.QueryBuilder()
                .WithAll<DestroyEntity>()
                .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
            foreach (var kvPair in map)
            {
                kvPair.Value.Dispose();
            }
            
            map.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                if (!map.TryGetValue(entity, out var list))
                    continue;
                
                foreach (var cleanup in list)
                {
                    if (cleanup.DestroyMethod == ChildDestroyMethod.Destroy)
                    {
                        Debug.Log($"Queuing entity {cleanup.Entity} for destroy");
                        state.EntityManager.SetComponentEnabled<DestroyEntity>(cleanup.Entity, true);
                    }
                    else
                    {
                        Debug.Log($"Queuing entity {cleanup.Entity} for cleanup");
                        state.EntityManager.SetComponentEnabled<NZCleanupEntity>(cleanup.Entity, true);
                    }
                }

                list.Dispose();
                map.Remove(entity);
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
                Debug.Log("Running cleanupQuery");
                state.EntityManager.SetComponentEnabled<NZCleanupEntity>(cleanupQuery, false);
            }
            
            if (!destroyQuery.IsEmpty)
            {
                Debug.Log("Running destroyQuery");
                state.EntityManager.DestroyEntity(destroyQuery);
            }
        }
    }
}