using System;
using Unity.Collections;
using UnityUnsafe = Unity.Collections.LowLevel.Unsafe;

namespace NZCore.Internal
{
    public unsafe struct UnsafeParallelHashMapBase<TKey, TValue> where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged
    {
        public static void Clear(UnsafeParallelHashMapData* data) =>
            UnityUnsafe.UnsafeParallelHashMapBase<TKey, TValue>.Clear((UnityUnsafe.UnsafeParallelHashMapData*)data);

        public static int AllocEntry(UnsafeParallelHashMapData* data, int threadIndex) =>
            UnityUnsafe.UnsafeParallelHashMapBase<TKey, TValue>.AllocEntry((UnityUnsafe.UnsafeParallelHashMapData*)data, threadIndex);

        public static void FreeEntry(UnsafeParallelHashMapData* data, int index, int threadIndex) =>
            UnityUnsafe.UnsafeParallelHashMapBase<TKey, TValue>.FreeEntry((UnityUnsafe.UnsafeParallelHashMapData*)data, index, threadIndex);

        public static bool TryAddAtomic(UnsafeParallelHashMapData* data, TKey key, TValue item, int threadIndex) =>
            UnityUnsafe.UnsafeParallelHashMapBase<TKey, TValue>.TryAddAtomic((UnityUnsafe.UnsafeParallelHashMapData*)data, key, item, threadIndex);

        public static void AddAtomicMulti(UnsafeParallelHashMapData* data, TKey key, TValue item, int threadIndex) =>
            UnityUnsafe.UnsafeParallelHashMapBase<TKey, TValue>.AddAtomicMulti((UnityUnsafe.UnsafeParallelHashMapData*)data, key, item, threadIndex);

        public static bool TryAdd(UnsafeParallelHashMapData* data, TKey key, TValue item, bool isMultiHashMap, AllocatorManager.AllocatorHandle allocator) =>
            UnityUnsafe.UnsafeParallelHashMapBase<TKey, TValue>.TryAdd((UnityUnsafe.UnsafeParallelHashMapData*)data, key, item, isMultiHashMap, allocator);

        public static int Remove(UnsafeParallelHashMapData* data, TKey key, bool isMultiHashMap) =>
            UnityUnsafe.UnsafeParallelHashMapBase<TKey, TValue>.Remove((UnityUnsafe.UnsafeParallelHashMapData*)data, key, isMultiHashMap);

        public static void Remove(UnsafeParallelHashMapData* data, NativeParallelMultiHashMapIterator<TKey> iterator) =>
            UnityUnsafe.UnsafeParallelHashMapBase<TKey, TValue>.Remove((UnityUnsafe.UnsafeParallelHashMapData*)data, iterator.Self);

        public static void RemoveKeyValue<TValueEQ>(UnsafeParallelHashMapData* data, TKey key, TValueEQ value)
            where TValueEQ : unmanaged, IEquatable<TValueEQ> =>
            UnityUnsafe.UnsafeParallelHashMapBase<TKey, TValue>.RemoveKeyValue((UnityUnsafe.UnsafeParallelHashMapData*)data, key, value);

        public static bool TryGetFirstValueAtomic(UnsafeParallelHashMapData* data, TKey key, out TValue item,
            out NativeParallelMultiHashMapIterator<TKey> iterator)
        {
            iterator = default;
            return UnityUnsafe.UnsafeParallelHashMapBase<TKey, TValue>.TryGetFirstValueAtomic((UnityUnsafe.UnsafeParallelHashMapData*)data, key, out item,
                out iterator.Self);
        }

        public static bool TryGetNextValueAtomic(UnsafeParallelHashMapData* data, out TValue item, ref NativeParallelMultiHashMapIterator<TKey> iterator) =>
            UnityUnsafe.UnsafeParallelHashMapBase<TKey, TValue>.TryGetNextValueAtomic((UnityUnsafe.UnsafeParallelHashMapData*)data, out item,
                ref iterator.Self);

        public static bool SetValue(UnsafeParallelHashMapData* data, ref NativeParallelMultiHashMapIterator<TKey> iterator, ref TValue item) =>
            UnityUnsafe.UnsafeParallelHashMapBase<TKey, TValue>.SetValue((UnityUnsafe.UnsafeParallelHashMapData*)data, ref iterator.Self, ref item);
    }
}