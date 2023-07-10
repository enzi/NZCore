using System;
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
                    return !hashmap.IsEmpty && hashmap.m_MultiHashMapData.TryGetFirstRefValue(ref value, ref iterator);
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

        private static unsafe bool TryGetFirstRefValue<TKey, TValue>(
            this UnsafeParallelMultiHashMap<TKey, TValue> hashMapData,
            ref byte* itemPtr, 
            ref NativeParallelMultiHashMapIterator<TKey> it)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var data = hashMapData.m_Buffer;

            if (data->allocatedIndexLength <= 0)
            {
                itemPtr = null;
                return false;
            }

            // First find the slot based on the hash
            int bucket = it.key.GetHashCode() & data->bucketCapacityMask;
            int* buckets = (int*)data->buckets;
            it.EntryIndex = it.NextEntryIndex = buckets[bucket];
            return hashMapData.TryGetNextRefValue(ref itemPtr, ref it);
        }

        private static unsafe bool TryGetNextRefValue<TKey, TValue>(
            this UnsafeParallelMultiHashMap<TKey, TValue> hashMapData,
            ref byte* itemPtr,
            ref NativeParallelMultiHashMapIterator<TKey> it)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            //CheckRead();
            int entryIdx = it.NextEntryIndex;
            it.NextEntryIndex = -1;
            it.EntryIndex = -1;
            itemPtr = null;

            var data = hashMapData.m_Buffer;
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
