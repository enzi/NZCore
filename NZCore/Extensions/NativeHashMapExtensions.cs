// <copyright project="NZCore" file="NativeHashMapExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Burst;
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

            item = null;
            return false;
        }

        public static unsafe void SetLength<TKey, TValue>(
            [NoAlias] this NativeHashMap<TKey, TValue> hashMap, int newLength)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            hashMap.m_Data->Count = newLength;
            hashMap.m_Data->AllocatedIndex = newLength;
        }
        
        public static unsafe void RecalculateBuckets<TKey, TValue>(
            [NoAlias] this NativeHashMap<TKey, TValue> hashMap)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(hashMap.m_Safety);
#endif
            var length = hashMap.Count;

            int* buckets = hashMap.m_Data->Buckets;
            int* nextPtrs = hashMap.m_Data->Next;
            TKey* keys = hashMap.m_Data->Keys;
            var bucketCapacityMask = hashMap.m_Data->BucketCapacity - 1;

            for (var idx = 0; idx < length; idx++)
            {
                var bucket = (int) ((uint)keys[idx].GetHashCode() & bucketCapacityMask);
                nextPtrs[idx] = buckets[bucket];
                buckets[bucket] = idx;
            }
        }
    }
}