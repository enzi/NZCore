// <copyright project="NZCore" file="ArenaBufferCommandExtensions.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    public static unsafe class ArenaBufferCommandExtensions
    {
        public static DynamicBuffer<T> AddArenaBuffer<T>(
            this EntityManager entityManager,
            Entity entity,
            int capacity = 8)
            where T : unmanaged, IArenaBuffer
        {
            var refType = ComponentType.FromTypeIndex(ArenaBufferStorage<T>.RefTypeIndex.Data);
            entityManager.AddComponent(entity, refType);

            var refData = (ArenaBufferRefData*)entityManager.GetComponentDataRaw(
                ArenaBufferStorage<T>.RefTypeIndex.Data,
                entity,
                false);
            refData->Block = (System.IntPtr)ArenaBufferRefData.Unreserved;
            refData->Length = 0;
            refData->Capacity = capacity;

            return AddStaging<T>(entityManager, entity, capacity);
        }

        public static DynamicBuffer<T> SetArenaBuffer<T>(
            this EntityManager entityManager,
            Entity entity,
            int capacity = 8)
            where T : unmanaged, IArenaBuffer
        {
            return AddStaging<T>(entityManager, entity, capacity);
        }

        public static DynamicBuffer<T> AddArenaBuffer<T>(
            this EntityCommandBuffer commandBuffer,
            Entity entity,
            int capacity = 8)
            where T : unmanaged, IArenaBuffer
        {
            commandBuffer.AddComponent(
                entity,
                ComponentType.FromTypeIndex(ArenaBufferStorage<T>.RefTypeIndex.Data));
            return SetArenaBuffer<T>(commandBuffer, entity, capacity);
        }

        public static DynamicBuffer<T> SetArenaBuffer<T>(
            this EntityCommandBuffer commandBuffer,
            Entity entity,
            int capacity = 8)
            where T : unmanaged, IArenaBuffer
        {
            var staging = commandBuffer.AddBuffer<ArenaBufferStaging<T>>(entity);
            staging.EnsureCapacity(capacity);
            return staging.Reinterpret<T>();
        }

        public static DynamicBuffer<T> AddArenaBuffer<T>(
            this EntityCommandBuffer.ParallelWriter commandBuffer,
            int sortKey,
            Entity entity,
            int capacity = 8)
            where T : unmanaged, IArenaBuffer
        {
            commandBuffer.AddComponent(
                sortKey,
                entity,
                ComponentType.FromTypeIndex(ArenaBufferStorage<T>.RefTypeIndex.Data));
            return SetArenaBuffer<T>(commandBuffer, sortKey, entity, capacity);
        }

        public static DynamicBuffer<T> SetArenaBuffer<T>(
            this EntityCommandBuffer.ParallelWriter commandBuffer,
            int sortKey,
            Entity entity,
            int capacity = 8)
            where T : unmanaged, IArenaBuffer
        {
            var staging = commandBuffer.AddBuffer<ArenaBufferStaging<T>>(sortKey, entity);
            staging.EnsureCapacity(capacity);
            return staging.Reinterpret<T>();
        }

        private static DynamicBuffer<T> AddStaging<T>(
            EntityManager entityManager,
            Entity entity,
            int capacity)
            where T : unmanaged, IArenaBuffer
        {
            var staging = entityManager.AddBuffer<ArenaBufferStaging<T>>(entity);
            staging.EnsureCapacity(capacity);
            return staging.Reinterpret<T>();
        }
    }
}
