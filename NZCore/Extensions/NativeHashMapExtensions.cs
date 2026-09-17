// <copyright project="NZCore" file="NativeHashMapExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.Internal;
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
            var idx = hashMap.GetData()->Find(key);

            if (-1 != idx)
            {
                item = (TValue*)hashMap.GetData()->Ptr + idx;
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
            hashMap.GetData()->Count = newLength;
            hashMap.GetData()->AllocatedIndex = newLength;
        }

        public static unsafe void RecalculateBuckets<TKey, TValue>(
            [NoAlias] this NativeHashMap<TKey, TValue> hashMap)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(hashMap.GetSafety());
#endif
            var length = hashMap.Count;

            var buckets = hashMap.GetData()->Buckets;
            var nextPtrs = hashMap.GetData()->Next;
            var keys = hashMap.GetData()->Keys;
            var bucketCapacityMask = hashMap.GetData()->BucketCapacity - 1;

            for (var idx = 0; idx < length; idx++)
            {
                var bucket = (int)((uint)keys[idx].GetHashCode() & bucketCapacityMask);
                nextPtrs[idx] = buckets[bucket];
                buckets[bucket] = idx;
            }
        }
    }
}
