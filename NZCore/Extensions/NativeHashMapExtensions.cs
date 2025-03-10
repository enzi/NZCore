﻿// <copyright project="NZCore" file="NativeHashMapExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    public static class NativeHashMapExtensions
    {
        public static unsafe bool TryGetRefValue<TKey, TValue>(this NativeHashMap<TKey, TValue> hashMap, TKey key, out TValue* item)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var idx = hashMap.m_Data->Find(key);

            if (-1 != idx)
            {
                item = (TValue*)(hashMap.m_Data->Ptr + (UnsafeUtility.SizeOf<TValue>() * idx));
                return true;
            }

            item = default;
            return false;
        }

        public static unsafe bool TryGetRefValue<TKey, TValue>(this NativeParallelHashMap<TKey, TValue> hashMap, TKey key, out void* valuePtr)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return TryGetFirstRefValueAtomic<TKey, TValue>(hashMap.m_HashMapData.m_Buffer, key, out valuePtr, out var _);
        }

        internal static unsafe bool TryGetFirstRefValueAtomic<TKey, TValue>(UnsafeParallelHashMapData* data, TKey key, out void* valuePtr, out NativeParallelMultiHashMapIterator<TKey> it)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            it.key = key;

            if (data->allocatedIndexLength <= 0)
            {
                it.EntryIndex = it.NextEntryIndex = -1;
                valuePtr = null;
                return false;
            }

            // First find the slot based on the hash
            int* buckets = (int*)data->buckets;
            int bucket = key.GetHashCode() & data->bucketCapacityMask;
            it.EntryIndex = it.NextEntryIndex = buckets[bucket];
            return TryGetNextRefValueAtomic<TKey, TValue>(data, out valuePtr, ref it);
        }

        internal static unsafe bool TryGetNextRefValueAtomic<TKey, TValue>(UnsafeParallelHashMapData* data, out void* valuePtr, ref NativeParallelMultiHashMapIterator<TKey> it)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            int entryIdx = it.NextEntryIndex;
            it.NextEntryIndex = -1;
            it.EntryIndex = -1;
            valuePtr = null;
            if (entryIdx < 0 || entryIdx >= data->keyCapacity)
            {
                return false;
            }

            int* nextPtrs = (int*)data->next;
            while (!UnsafeUtility.ReadArrayElement<TKey>(data->keys, entryIdx).Equals(it.key))
            {
                entryIdx = nextPtrs[entryIdx];
                if (entryIdx < 0 || entryIdx >= data->keyCapacity)
                {
                    return false;
                }
            }

            it.NextEntryIndex = nextPtrs[entryIdx];
            it.EntryIndex = entryIdx;

            valuePtr = data->values + entryIdx * sizeof(TValue);

            return true;
        }
    }
}