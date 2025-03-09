// <copyright project="NZCore" file="NativeParallelMultiHashMapExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    public static class NativeParallelMultiHashMapExtensions
    {
        public static unsafe bool ContainsKeyFast<TKey, TValue>(
            this NativeParallelMultiHashMap<TKey, TValue> hashmap,
            TKey key)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return TryPeekFirstRefValue(hashmap.m_MultiHashMapData.m_Buffer, key);
        }

        private static unsafe bool TryPeekFirstRefValue<TKey>(UnsafeParallelHashMapData* data, TKey key)
            where TKey : unmanaged, IEquatable<TKey>
        {
            if (data->allocatedIndexLength <= 0)
                return false;

            // First find the slot based on the hash
            int* buckets = (int*)data->buckets;
            int bucket = key.GetHashCode() & data->bucketCapacityMask;
            return TryPeekNextRefValue(data, key, buckets[bucket]);
        }

        private static unsafe bool TryPeekNextRefValue<TKey>(UnsafeParallelHashMapData* data, TKey key, int entryIdx)
            where TKey : unmanaged, IEquatable<TKey>
        {
            if (entryIdx < 0 || entryIdx >= data->keyCapacity)
                return false;

            int* nextPtrs = (int*)data->next;
            while (!(*(TKey*)(data->keys + entryIdx * sizeof(TKey))).Equals(key))
            {
                entryIdx = nextPtrs[entryIdx];
                if (entryIdx < 0 || entryIdx >= data->keyCapacity)
                {
                    return false;
                }
            }

            return true;
        }

        public static unsafe void RecalculateBuckets<TKey, TValue>(
            [NoAlias] this NativeParallelMultiHashMap<TKey, TValue> hashMap)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(hashMap.m_Safety);
#endif
            var length = hashMap.m_MultiHashMapData.m_Buffer->allocatedIndexLength;

            var data = hashMap.GetUnsafeBucketData();
            var buckets = (int*)data.buckets;
            var nextPtrs = (int*)data.next;
            var keys = (TKey*)data.keys;

            for (var idx = 0; idx < length; idx++)
            {
                var bucket = keys[idx].GetHashCode() & data.bucketCapacityMask;
                nextPtrs[idx] = buckets[bucket];
                buckets[bucket] = idx;
            }
        }

        public static unsafe void CalculateBuckets<TKey, TValue>(this NativeParallelMultiHashMap<TKey, TValue> hashMap, NativeArray<TKey> keys)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            UnsafeParallelHashMapBucketData data = hashMap.GetUnsafeBucketData();

            var buckets = (int*)data.buckets;
            var nextPtrs = (int*)data.next;

            for (var idx = 0; idx < keys.Length; idx++)
            {
                var bucket = keys[idx].GetHashCode() & data.bucketCapacityMask;
                nextPtrs[idx] = buckets[bucket];
                buckets[bucket] = idx;
            }

            hashMap.m_MultiHashMapData.m_Buffer->allocatedIndexLength = keys.Length;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static unsafe void AddBatchUnsafe<TKey, TValue>(
            [NoAlias] this NativeParallelMultiHashMap<TKey, TValue>.ParallelWriter hashMap,
            [NoAlias] NativeArray<TKey> keys,
            [NoAlias] NativeArray<TValue> values)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            //CheckLengthsMatch(keys.Length, values.Length);
            AddBatchUnsafe(hashMap, (TKey*)keys.GetUnsafeReadOnlyPtr(), (TValue*)values.GetUnsafeReadOnlyPtr(), keys.Length);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static unsafe void AddBatchUnsafe<TKey, TValue>(
            [NoAlias] this NativeParallelMultiHashMap<TKey, TValue>.ParallelWriter hashMap,
            [NoAlias] TKey* keys,
            [NoAlias] TValue* values,
            int length) where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged
        {
            hashMap.m_Writer.m_Buffer->AddBatchUnsafeParallel(keys, values, length);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static unsafe void AddBatchUnsafeParallel<TKey, TValue>(
            [NoAlias] this ref UnsafeParallelHashMapData data,
            [NoAlias] TKey* keys,
            [NoAlias] TValue* values,
            int length)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var newLength = Interlocked.Add(ref data.allocatedIndexLength, length);
            var oldLength = newLength - length;

            var keyPtr = (TKey*)data.keys + oldLength;
            var valuePtr = (TValue*)data.values + oldLength;

            UnsafeUtility.MemCpy(keyPtr, keys, length * UnsafeUtility.SizeOf<TKey>());
            UnsafeUtility.MemCpy(valuePtr, values, length * UnsafeUtility.SizeOf<TValue>());

            var buckets = (int*)data.buckets;
            var nextPtrs = (int*)data.next + oldLength;

            for (var idx = 0; idx < length; idx++)
            {
                var hash = keys[idx].GetHashCode() & data.bucketCapacityMask;
                var index = oldLength + idx;
                var next = Interlocked.Exchange(ref UnsafeUtility.ArrayElementAsRef<int>(buckets, hash), index);
                nextPtrs[idx] = next;
            }
        }

        public static unsafe void AddBatchUnsafe<TKey, TValue>(
            [NoAlias] this NativeParallelMultiHashMap<TKey, TValue> hashMap,
            [NoAlias] NativeArray<TKey> keys,
            [NoAlias] NativeArray<TValue> values)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            AddBatchUnsafe(hashMap, (TKey*)keys.GetUnsafeReadOnlyPtr(), (TValue*)values.GetUnsafeReadOnlyPtr(), keys.Length);
        }

        public static unsafe void AddBatchUnsafe<TKey, TValue>(
            [NoAlias] this NativeParallelMultiHashMap<TKey, TValue> hashMap,
            [NoAlias] TKey* keys,
            [NoAlias] TValue* values,
            int length)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(hashMap.m_Safety);
#endif

            var oldLength = hashMap.Count();
            var newLength = oldLength + length;

            if (hashMap.Capacity < newLength)
            {
                hashMap.Capacity = newLength;
            }

            var data = hashMap.GetUnsafeBucketData();

            var keyPtr = (TKey*)data.keys + oldLength;
            var valuePtr = (TValue*)data.values + oldLength;

            UnsafeUtility.MemCpy(keyPtr, keys, length * UnsafeUtility.SizeOf<TKey>());
            UnsafeUtility.MemCpy(valuePtr, values, length * UnsafeUtility.SizeOf<TValue>());

            var buckets = (int*)data.buckets;
            var nextPtrs = (int*)data.next + oldLength;

            for (var idx = 0; idx < length; idx++)
            {
                var bucket = keys[idx].GetHashCode() & data.bucketCapacityMask;
                nextPtrs[idx] = buckets[bucket];
                buckets[bucket] = oldLength + idx;
            }

            hashMap.m_MultiHashMapData.m_Buffer->allocatedIndexLength += length;
        }

        public static unsafe void SetAllocatedIndexLength<TKey, TValue>([NoAlias] this NativeParallelMultiHashMap<TKey, TValue> hashMap, int length)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            hashMap.m_MultiHashMapData.m_Buffer->allocatedIndexLength = length;
        }

        public static RefEnumerator<TKey, TValue> GetRefValuesForKey<TKey, TValue>(
            this NativeParallelMultiHashMap<TKey, TValue> hashmap,
            TKey key)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(hashmap.m_Safety);
#endif
            return new RefEnumerator<TKey, TValue>(hashmap, key);
        }

        public unsafe struct RefEnumerator<TKey, TValue>
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            private readonly NativeParallelMultiHashMap<TKey, TValue> hashmap;
            private NativeParallelMultiHashMapIterator<TKey> iterator;
            private byte* value;
            private bool isFirst;

            public RefEnumerator(NativeParallelMultiHashMap<TKey, TValue> hashmap, TKey key)
            {
                isFirst = true;
                this.hashmap = hashmap;
                iterator = new NativeParallelMultiHashMapIterator<TKey>()
                {
                    key = key,
                    EntryIndex = -1,
                    NextEntryIndex = -1
                };

                value = null;
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (isFirst)
                {
                    isFirst = false;
                    return TryGetFirstRefValue<TKey, TValue>(hashmap.m_MultiHashMapData.m_Buffer, out value, ref iterator);
                }

                return TryGetNextRefValue<TKey, TValue>(hashmap.m_MultiHashMapData.m_Buffer, out value, ref iterator);
            }

            public void Reset() => isFirst = true;
            public ref TValue Current => ref UnsafeUtility.AsRef<TValue>(value);
        }

        private static unsafe bool TryGetFirstRefValue<TKey, TValue>(UnsafeParallelHashMapData* data, out byte* itemPtr, ref NativeParallelMultiHashMapIterator<TKey> it)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (data->allocatedIndexLength <= 0)
            {
                it.EntryIndex = it.NextEntryIndex = -1;
                itemPtr = null;
                return false;
            }

            // First find the slot based on the hash
            int* buckets = (int*)data->buckets;
            int bucket = it.key.GetHashCode() & data->bucketCapacityMask;
            it.EntryIndex = it.NextEntryIndex = buckets[bucket];
            return TryGetNextRefValue<TKey, TValue>(data, out itemPtr, ref it);
        }

        private static unsafe bool TryGetNextRefValue<TKey, TValue>(UnsafeParallelHashMapData* data, out byte* itemPtr, ref NativeParallelMultiHashMapIterator<TKey> it)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            int entryIdx = it.NextEntryIndex;
            it.NextEntryIndex = -1;
            it.EntryIndex = -1;
            itemPtr = null;

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

            // set the ptr
            itemPtr = data->values + entryIdx * sizeof(TValue);

            return true;
        }
    }
}