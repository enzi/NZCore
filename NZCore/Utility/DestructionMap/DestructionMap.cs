// <copyright project="NZCore" file="DestructionMap.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    public unsafe struct DestructionMap : IComponentData, IDisposable
    {
        public NativeHashMap<Entity, UnsafeList<DestroyMapElement>> Map;

        public void Allocate()
        {
            Map = new NativeHashMap<Entity, UnsafeList<DestroyMapElement>>(0, Allocator.Persistent);
        }

        public void Add(Entity parent, Entity child, ChildDestroyMethod destroyMethod, byte payLoad = 0)
        {
            //Debug.Log($"Adding {child} to {parent} - {destroyMethod}");

            if (Map.TryGetRefValue(parent, out var listPtr))
            {
                listPtr->Add(new DestroyMapElement()
                {
                    Entity = child,
                    DestroyMethod = destroyMethod,
                    Payload = payLoad
                });
            }
            else
            {
                var newList = new UnsafeList<DestroyMapElement>(1, Allocator.Persistent);
                newList.Add(new DestroyMapElement()
                {
                    Entity = child,
                    DestroyMethod = destroyMethod,
                    Payload = payLoad
                });

                Map.Add(parent, newList);
            }
        }

        public void Add(ref SystemState state, Entity parent, Entity child, ChildDestroyMethod destroyMethod, byte payLoad = 0)
        {
            switch (destroyMethod)
            {
                case ChildDestroyMethod.Destroy:
                {
                    if (!state.EntityManager.HasComponent<DestroyEntity>(child))
                        state.EntityManager.AddComponent<DestroyEntity>(child);

                    state.EntityManager.SetComponentEnabled<DestroyEntity>(child, false);

                    break;
                }
                case ChildDestroyMethod.Cleanup:
                {
                    if (!state.EntityManager.HasComponent<NZCleanupEntity>(child))
                        state.EntityManager.AddComponent<NZCleanupEntity>(child);

                    state.EntityManager.SetComponentEnabled<NZCleanupEntity>(child, false);

                    break;
                }
            }

            Add(parent, child, destroyMethod, payLoad);
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

        public void Remove(Entity parent, Entity child)
        {
            if (!Map.TryGetRefValue(parent, out var listPtr))
                return;

            var startIndex = listPtr->Length - 1;
            for (int i = startIndex; i >= 0; i--)
            {
                if (listPtr->ElementAt(i).Entity == child)
                    listPtr->RemoveAtSwapBack(i);
            }
        }

        // public void AddDeferred(Entity parent, Entity child, ChildDestroyMethod destroyMethod, byte payload = 0)
        // {
        //     DeferredMap.
        //     
        //     if (DeferredMap.TryGetRefValue(parent, out var listPtr))
        //     {
        //         listPtr->Add(new DestroyMapElement()
        //         {
        //             Entity = child,
        //             DestroyMethod = destroyMethod,
        //             Payload = payload
        //         });
        //     }
        //     else
        //     {
        //         var newList = new UnsafeList<DestroyMapElement>(1, Allocator.Persistent);
        //         newList.Add(new DestroyMapElement()
        //         {
        //             Entity = child,
        //             DestroyMethod = destroyMethod,
        //             Payload = payload
        //         });
        //         
        //         DeferredMap.Add(parent, newList);
        //     }
        // }

        public void Dispose()
        {
            foreach (var kvPair in Map)
            {
                kvPair.Value.Dispose();
            }

            Map.Dispose();
        }
    }
}