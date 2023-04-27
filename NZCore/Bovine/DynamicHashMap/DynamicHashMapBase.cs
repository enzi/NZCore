﻿// <copyright file="DynamicHashMapBase.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace NZCore.Core.Iterators
{
    using System;
    using System.Diagnostics;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Entities;

    internal unsafe struct DynamicHashMapBase<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        internal static void Clear(DynamicBuffer<byte> buffer)
        {
            var data = buffer.AsData<TKey, TValue>();

            UnsafeUtility.MemSet(data->Buckets, 0xff, (data->BucketCapacityMask + 1) * 4);
            UnsafeUtility.MemSet(data->Next, 0xff, data->KeyCapacity * 4);

            data->FirstFreeIDX = -1;

            data->AllocatedIndexLength = 0;
        }

        internal static bool TryAdd(DynamicBuffer<byte> buffer, TKey key, TValue item, bool isMultiHashMap)
        {
            var data = buffer.AsData<TKey, TValue>();

            if (!isMultiHashMap && TryGetFirstValueAtomic(data, key, out _, out _))
            {
                return false;
            }

            // Allocate an entry from the free list
            if (data->AllocatedIndexLength >= data->KeyCapacity && data->FirstFreeIDX < 0)
            {
                int newCap = DynamicHashMapData.GrowCapacity(data->KeyCapacity);
                DynamicHashMapData.ReallocateHashMap<TKey, TValue>(buffer, newCap, DynamicHashMapData.GetBucketSize(newCap), out data);
            }

            var idx = data->FirstFreeIDX;

            if (idx >= 0)
            {
                data->FirstFreeIDX = ((int*)data->Next)[idx];
            }
            else
            {
                idx = data->AllocatedIndexLength++;
            }

            CheckIndexOutOfBounds(data, idx);

            // Write the new value to the entry
            UnsafeUtility.WriteArrayElement(data->Keys, idx, key);
            UnsafeUtility.WriteArrayElement(data->Values, idx, item);

            int bucket = key.GetHashCode() & data->BucketCapacityMask;

            // Add the index to the hash-map
            int* buckets = (int*)data->Buckets;
            var nextPtrs = (int*)data->Next;

            nextPtrs[idx] = buckets[bucket];
            buckets[bucket] = idx;

            return true;
        }

        internal static int Remove(DynamicBuffer<byte> buffer, TKey key, bool isMultiHashMap)
        {
            var data = buffer.AsData<TKey, TValue>();

            if (data->KeyCapacity == 0)
            {
                return 0;
            }

            var removed = 0;

            // First find the slot based on the hash
            var buckets = (int*)data->Buckets;
            var nextPtrs = (int*)data->Next;
            var bucket = key.GetHashCode() & data->BucketCapacityMask;
            var prevEntry = -1;
            var entryIdx = buckets[bucket];

            while (entryIdx >= 0 && entryIdx < data->KeyCapacity)
            {
                if (UnsafeUtility.ReadArrayElement<TKey>(data->Keys, entryIdx).Equals(key))
                {
                    ++removed;

                    // Found matching element, remove it
                    if (prevEntry < 0)
                    {
                        buckets[bucket] = nextPtrs[entryIdx];
                    }
                    else
                    {
                        nextPtrs[prevEntry] = nextPtrs[entryIdx];
                    }

                    // And free the index
                    int nextIdx = nextPtrs[entryIdx];
                    nextPtrs[entryIdx] = data->FirstFreeIDX;
                    data->FirstFreeIDX = entryIdx;
                    entryIdx = nextIdx;

                    // Can only be one hit in regular hashmaps, so return
                    if (!isMultiHashMap)
                    {
                        break;
                    }
                }
                else
                {
                    prevEntry = entryIdx;
                    entryIdx = nextPtrs[entryIdx];
                }
            }

            return removed;
        }

        internal static bool SetValue(DynamicBuffer<byte> buffer, ref NativeParallelMultiHashMapIterator<TKey> it, ref TValue item)
        {
            var data = buffer.AsData<TKey, TValue>();

            int entryIdx = it.EntryIndex;
            if (entryIdx < 0 || entryIdx >= data->KeyCapacity)
            {
                return false;
            }

            UnsafeUtility.WriteArrayElement(data->Values, entryIdx, item);
            return true;
        }

        internal static bool TryGetFirstValueAtomic(DynamicHashMapData* data, TKey key, out TValue item, out NativeParallelMultiHashMapIterator<TKey> it)
        {
            it.key = key;

            if (data->AllocatedIndexLength <= 0)
            {
                it.EntryIndex = it.NextEntryIndex = -1;
                item = default;
                return false;
            }

            // First find the slot based on the hash
            int* buckets = (int*)data->Buckets;
            int bucket = key.GetHashCode() & data->BucketCapacityMask;
            it.EntryIndex = it.NextEntryIndex = buckets[bucket];
            return TryGetNextValueAtomic(data, out item, ref it);
        }

        internal static bool TryGetNextValueAtomic(DynamicHashMapData* data, out TValue item, ref NativeParallelMultiHashMapIterator<TKey> it)
        {
            int entryIdx = it.NextEntryIndex;
            it.NextEntryIndex = -1;
            it.EntryIndex = -1;
            item = default;
            if (entryIdx < 0 || entryIdx >= data->KeyCapacity)
            {
                return false;
            }

            int* nextPtrs = (int*)data->Next;
            while (!UnsafeUtility.ReadArrayElement<TKey>(data->Keys, entryIdx).Equals(it.key))
            {
                entryIdx = nextPtrs[entryIdx];
                if (entryIdx < 0 || entryIdx >= data->KeyCapacity)
                {
                    return false;
                }
            }

            it.NextEntryIndex = nextPtrs[entryIdx];
            it.EntryIndex = entryIdx;

            // Read the value
            item = UnsafeUtility.ReadArrayElement<TValue>(data->Values, entryIdx);

            return true;
        }

        internal static void RemoveKeyValue<TValueEq>(DynamicHashMapData* data, TKey key, TValueEq value)
            where TValueEq : unmanaged, IEquatable<TValueEq>
        {
            if (data->KeyCapacity == 0)
            {
                return;
            }

            var buckets = (int*)data->Buckets;
            var keyCapacity = (uint)data->KeyCapacity;
            var prevNextPtr = buckets + (key.GetHashCode() & data->BucketCapacityMask);
            var entryIdx = *prevNextPtr;

            if ((uint)entryIdx >= keyCapacity)
            {
                return;
            }

            var nextPtrs = (int*)data->Next;
            var keys = data->Keys;
            var values = data->Values;

            do
            {
                if (UnsafeUtility.ReadArrayElement<TKey>(keys, entryIdx).Equals(key)
                    && UnsafeUtility.ReadArrayElement<TValueEq>(values, entryIdx).Equals(value))
                {
                    var nextIdx = nextPtrs[entryIdx];
                    nextPtrs[entryIdx] = data->FirstFreeIDX;
                    data->FirstFreeIDX = entryIdx;
                    *prevNextPtr = entryIdx = nextIdx;
                }
                else
                {
                    prevNextPtr = nextPtrs + entryIdx;
                    entryIdx = *prevNextPtr;
                }
            }
            while ((uint)entryIdx < keyCapacity);
        }

        internal static bool FindFirst<TValueEq>(DynamicHashMapData* data, TKey key, TValueEq value, out TValueEq* result)
            where TValueEq : unmanaged, IEquatable<TValueEq>
        {
            result = null;

            if (data->KeyCapacity == 0)
            {
                return false;
            }

            var buckets = (int*)data->Buckets;
            var keyCapacity = (uint)data->KeyCapacity;
            var prevNextPtr = buckets + (key.GetHashCode() & data->BucketCapacityMask);
            var entryIdx = *prevNextPtr;

            if ((uint)entryIdx >= keyCapacity)
            {
                return false;
            }

            var nextPtrs = (int*)data->Next;
            var keys = data->Keys;
            var values = data->Values;

            do
            {
                if (UnsafeUtility.ReadArrayElement<TKey>(keys, entryIdx).Equals(key)
                    && UnsafeUtility.ReadArrayElement<TValueEq>(values, entryIdx).Equals(value))
                {
                    result = (TValueEq*)(values + (entryIdx * sizeof(TValueEq)));
                    return true;
                }

                prevNextPtr = nextPtrs + entryIdx;
                entryIdx = *prevNextPtr;
            }
            while ((uint)entryIdx < keyCapacity);

            return false;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndexOutOfBounds(DynamicHashMapData* data, int idx)
        {
            if (idx < 0 || idx >= data->KeyCapacity)
            {
                throw new InvalidOperationException("Internal HashMap error");
            }
        }
    }
}
