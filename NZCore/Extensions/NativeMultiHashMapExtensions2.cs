using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    public static class NativeMultiHashMapExtensions2
    {
        public static bool ContainsKeyFast<TKey, TValue>(
            this NativeParallelMultiHashMap<TKey, TValue> hashmap,
            TKey key)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return hashmap.m_MultiHashMapData.TryPeekFirstRefValue(key);
        }

        public static unsafe bool TryPeekFirstRefValue<TKey, TValue>(
            this UnsafeParallelMultiHashMap<TKey, TValue> m_MultiHashMapData,
            TKey key)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var data = m_MultiHashMapData.m_Buffer;

            if (data->allocatedIndexLength <= 0)
            {
                return false;
            }

            // First find the slot based on the hash
            int* buckets = (int*)data->buckets;
            int bucket = key.GetHashCode() & data->bucketCapacityMask;
            return m_MultiHashMapData.TryPeekNextRefValue(key, buckets[bucket]);
        }

        public static unsafe bool TryPeekNextRefValue<TKey, TValue>(
            this UnsafeParallelMultiHashMap<TKey, TValue> m_MultiHashMapData,
            TKey key,
            int entryIdx)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            //CheckRead();

            var data = m_MultiHashMapData.m_Buffer;
            if (entryIdx < 0 || entryIdx >= data->keyCapacity)
            {
                return false;
            }

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
        
        public static unsafe void CalculateBuckets<TKey, TValue>(this NativeParallelMultiHashMap<TKey, TValue> hashMap, NativeArray<TKey> keys)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var data = hashMap.GetUnsafeBucketData();

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
        
        public static RefEnumerator<TKey, TValue> GetRefValuesForKey<TKey, TValue>(
            this NativeParallelMultiHashMap<TKey, TValue> hashmap,
            TKey key)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(hashmap.m_Safety);
#endif
            return new RefEnumerator<TKey, TValue> { hashmap = hashmap, key = key, isFirst = true };
        }

        public unsafe struct RefEnumerator<TKey, TValue> 
            where TKey : unmanaged, IEquatable<TKey> 
            where TValue : unmanaged
        {
            public NativeParallelMultiHashMap<TKey, TValue> hashmap;
            public TKey key;
            public bool isFirst;

            private byte* value;
            private NativeParallelMultiHashMapIterator<TKey> iterator;

            public void Dispose() { }

            /// <summary>
            /// Advances the enumerator to the next value of the key.
            /// </summary>
            /// <returns>True if <see cref="Current"/> is valid to read after the call.</returns>
            public bool MoveNext()
            {
                if (isFirst)
                {
                    isFirst = false;                    
                    return hashmap.m_MultiHashMapData.TryGetFirstRefValue(key, ref value, out iterator);
                }

                return hashmap.m_MultiHashMapData.TryGetNextRefValue(ref value, ref iterator);
            }

            /// <summary>
            /// Resets the enumerator to its initial state.
            /// </summary>
            public void Reset() => isFirst = true;

            /// <summary>
            /// The current value.
            /// </summary>
            /// <value>The current value.</value>
            public ref TValue Current => ref UnsafeUtility.AsRef<TValue>(value);
        }

        public static unsafe bool TryGetFirstRefValue<TKey, TValue>(
            this UnsafeParallelMultiHashMap<TKey, TValue> m_MultiHashMapData,
            TKey key,
            ref byte* item, 
            out NativeParallelMultiHashMapIterator<TKey> it)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            it.key = key;
            var data = m_MultiHashMapData.m_Buffer;

            if (data->allocatedIndexLength <= 0)
            {
                it.EntryIndex = it.NextEntryIndex = -1;
                item = default;
                return false;
            }

            // First find the slot based on the hash
            int* buckets = (int*)data->buckets;
            int bucket = key.GetHashCode() & data->bucketCapacityMask;
            it.EntryIndex = it.NextEntryIndex = buckets[bucket];
            return m_MultiHashMapData.TryGetNextRefValue(ref item, ref it);
        }       

        public static unsafe bool TryGetNextRefValue<TKey, TValue>(
            this UnsafeParallelMultiHashMap<TKey, TValue> m_MultiHashMapData,
            ref byte* item,
            ref NativeParallelMultiHashMapIterator<TKey> it)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            //CheckRead();
            int entryIdx = it.NextEntryIndex;
            it.NextEntryIndex = -1;
            it.EntryIndex = -1;
            item = default;

            var data = m_MultiHashMapData.m_Buffer;
            if (entryIdx < 0 || entryIdx >= data->keyCapacity)
            {
                return false;
            }

            int* nextPtrs = (int*)data->next;
            //while (!(*(TKey*) (data->keys + entryIdx * sizeof(TKey))).Equals(it.key))
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

            // Read the value
            item = data->values + entryIdx * sizeof(TValue);

            return true;
        }
    }
}
