// <copyright project="NZCore" file="NativeParallelHashMapExtensions.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    public static class NativeParallelHashMapExtensions
    {
        public static unsafe bool ContainsKeyFast<TKey, TValue>(
            this NativeParallelHashMap<TKey, TValue> hashmap,
            TKey key)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return TryPeekFirstRefValue(hashmap.m_HashMapData.m_Buffer, key);
        }

        private static unsafe bool TryPeekFirstRefValue<TKey>(
            UnsafeParallelHashMapData* data,
            TKey key)
            where TKey : unmanaged, IEquatable<TKey>
        {
            if (data->allocatedIndexLength <= 0)
            {
                return false;
            }

            // First find the slot based on the hash
            int* buckets = (int*)data->buckets;
            int bucket = key.GetHashCode() & data->bucketCapacityMask;
            return TryPeekNextValueAtomic(data, key, buckets[bucket]);
        }

        private static unsafe bool TryPeekNextValueAtomic<TKey>(
            UnsafeParallelHashMapData* data,
            TKey key,
            int entryIdx)
            where TKey : unmanaged, IEquatable<TKey>
        {
            if (entryIdx < 0 || entryIdx >= data->keyCapacity)
            {
                return false;
            }

            int* nextPtrs = (int*)data->next;
            while (!UnsafeUtility.ReadArrayElement<TKey>(data->keys, entryIdx).Equals(key))
            {
                entryIdx = nextPtrs[entryIdx];
                if (entryIdx < 0 || entryIdx >= data->keyCapacity)
                {
                    return false;
                }
            }

            return true;
        }
    }
}