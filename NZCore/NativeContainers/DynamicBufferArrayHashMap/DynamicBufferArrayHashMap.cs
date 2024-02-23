using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NZCore.NativeContainers
{
    public unsafe struct DynamicBufferArrayHashMap<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        private DynamicBuffer<byte> buffer;

        public DynamicBufferArrayHashMap(DynamicBuffer<byte> buffer)
        {
            this.buffer = buffer;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetArrays<TKeyInterpreter>(DynamicBuffer<TKey> keyArray, DynamicBuffer<TValue> valueArray, TKeyInterpreter keyInterpreter)
            where TKeyInterpreter : unmanaged, IKeyInterpreter<TKey>
        {
            int length = keyArray.Length;
            int bucketLength = length * 2;
            
            var hashMapDataSize = UnsafeUtility.SizeOf<DynamicBufferArrayHashMapBaseData>();
            
            bucketLength = math.ceilpow2(bucketLength);
            int mask = bucketLength - 1;

            var totalLength = CalculateDataSize(length, bucketLength, out var bucketOffset);
            
            buffer.ResizeUninitialized(hashMapDataSize + totalLength);

            var bufferPtr = (byte*) buffer.GetUnsafePtr();
            var baseData = (DynamicBufferArrayHashMapBaseData*) bufferPtr;

            baseData->KeyArray = (byte*) keyArray.GetUnsafeReadOnlyPtr();
            baseData->ValueArray = (byte*) valueArray.GetUnsafeReadOnlyPtr();
            
            baseData->KeyCapacity = length;
            baseData->BucketCapacityMask = mask;
            baseData->AllocatedIndexLength = length;

            baseData->NextOffset = hashMapDataSize;
            baseData->BucketsOffset =  hashMapDataSize + bucketOffset;
            
            UnsafeUtility.MemSet(bufferPtr + baseData->NextOffset, 0xFF, (length + bucketLength) * 8); // sets everything to max, Unity uses the same method in their NativeHashMap clear
            
            // calculate buckets
            int valueIndex = 0;
            var next = (DynamicValueIndexer*) bufferPtr + hashMapDataSize;
            var buckets = (DynamicValueIndexer*) bufferPtr + hashMapDataSize + bucketOffset;

            for (int i = 0; i < length; i++)
            {
                TKey key = keyArray[i];
                var bucketIndex = key.GetHashCode() & mask;

                next[i] = buckets[bucketIndex];
                buckets[bucketIndex] = new DynamicValueIndexer()
                {
                    Index = i,
                    ValueIndex = valueIndex
                };

                valueIndex += keyInterpreter.GetSize(key);
            }
        }

        public bool ContainsKey(TKey key)
        {
            return TryPeekFirstValueAtomic(AsDataReadOnly(buffer), key);
        }
        
        public DynamicBufferArrayHashMapEnumerator<TKey, TValue> GetEnumerator(TKey key)
        {
            return new DynamicBufferArrayHashMapEnumerator<TKey, TValue>()
            {
                IsFirst = true,
                Key = key,
                Map = AsDataReadOnly(buffer)
            };
        }

        internal static bool TryGetFirstRefValue(DynamicBufferArrayHashMapBaseData* data, TKey key, out byte* item, out DynamicBufferArrayHashMapIterator<TKey> it)
        {
            it.Key = key;
            
            if (data->AllocatedIndexLength <= 0)
            {
                it.EntryIndex = it.NextEntryIndex = DynamicValueIndexer.Null;
                item = null;
                return false;
            }

            // First find the slot based on the hash    
            var buckets = GetBuckets(data);
            int bucket = key.GetHashCode() & data->BucketCapacityMask;
            it.EntryIndex = it.NextEntryIndex = buckets[bucket];
            return TryGetNextRefValue(data, out item, ref it);
        }
        
        internal static bool TryGetNextRefValue(DynamicBufferArrayHashMapBaseData* data, out byte* item, ref DynamicBufferArrayHashMapIterator<TKey> it)
        {
            var entryIdx = it.NextEntryIndex;
            it.NextEntryIndex = DynamicValueIndexer.Null;
            it.EntryIndex = DynamicValueIndexer.Null;
            item = null;

            if (entryIdx.Index < 0 || entryIdx.Index >= data->KeyCapacity)
            {
                return false;
            }

            var keys = (TKey*) data->KeyArray;
            var next = GetNexts(data);
            while (!keys[entryIdx.Index].Equals(it.Key))
            {
                entryIdx = next[entryIdx.Index];
                if (entryIdx.Index < 0 || entryIdx.Index >= data->KeyCapacity)
                {
                    return false;
                }
            }

            it.NextEntryIndex = next[entryIdx.Index];
            it.EntryIndex = entryIdx;

            // Read the value
            var values = data->ValueArray;
            item = values + entryIdx.ValueIndex;

            return true;
        }
        
        internal static bool TryPeekFirstValueAtomic(DynamicBufferArrayHashMapBaseData* data, TKey key)
        {
            if (data->AllocatedIndexLength <= 0)
                return false;

            // First find the slot based on the hash
            var buckets = GetBuckets(data);
            int bucket = key.GetHashCode() & data->BucketCapacityMask;
            return TryPeekNextValueAtomic(data, key, buckets[bucket]);
        }

        internal static bool TryPeekNextValueAtomic(DynamicBufferArrayHashMapBaseData* data, TKey key, DynamicValueIndexer entryIdx)
        {
            if (entryIdx.Index < 0 || entryIdx.Index >= data->KeyCapacity)
                return false;

            var keys = (TKey*) data->KeyArray;

            var nextPtrs = GetNexts(data);
            while (!UnsafeUtility.ReadArrayElement<TKey>(keys, entryIdx.Index).Equals(key))
            {
                entryIdx = nextPtrs[entryIdx.Index];
                if (entryIdx.Index < 0 || entryIdx.Index >= data->KeyCapacity)
                {
                    return false;
                }
            }

            return true;
        }
        
        private static int CalculateDataSize(int length, int bucketLength, out int bucketOffset)
        {
            var sizeOfDynamicValueIndexer = UnsafeUtility.SizeOf<DynamicValueIndexer>();
            
            var nextSize = CollectionHelper.Align(sizeOfDynamicValueIndexer * length, JobsUtility.CacheLineSize);
            var bucketSize = CollectionHelper.Align(sizeOfDynamicValueIndexer * bucketLength, JobsUtility.CacheLineSize);
            var totalSize = nextSize + bucketSize;
            
            bucketOffset = nextSize;

            return totalSize;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DynamicBufferArrayHashMapBaseData* AsData(DynamicBuffer<byte> buffer)
        {
            return (DynamicBufferArrayHashMapBaseData*) buffer.GetUnsafePtr();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DynamicBufferArrayHashMapBaseData* AsDataReadOnly(DynamicBuffer<byte> buffer)
        {
            return (DynamicBufferArrayHashMapBaseData*) buffer.GetUnsafeReadOnlyPtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DynamicValueIndexer* GetBuckets(DynamicBufferArrayHashMapBaseData* data)
        {
            return (DynamicValueIndexer*) ((byte*) data + data->BucketsOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DynamicValueIndexer* GetNexts(DynamicBufferArrayHashMapBaseData* data)
        {
            return (DynamicValueIndexer*) ((byte*) data + data->NextOffset);
        }
    }
    
    public unsafe struct DynamicBufferArrayHashMapEnumerator<TKey, TValue> 
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        public DynamicBufferArrayHashMapBaseData* Map;
        public TKey Key;
        public bool IsFirst;
        
        private DynamicBufferArrayHashMapIterator<TKey> iterator;
        private byte* value;

        public byte* Current => value;

        public bool MoveNext()
        {
            //Avoids going beyond the end of the collection.
            if (!IsFirst) 
                return DynamicBufferArrayHashMap<TKey, TValue>.TryGetNextRefValue(Map, out value, ref iterator);
            
            IsFirst = false;
            return DynamicBufferArrayHashMap<TKey, TValue>.TryGetFirstRefValue(Map, Key, out value, out iterator);

        }
    }

    public unsafe struct DynamicBufferArrayHashMapBaseData
    {
        public byte* KeyArray;
        public byte* ValueArray;
        public int NextOffset;
        public int BucketsOffset;
        
        internal int KeyCapacity;
        internal int BucketCapacityMask;
        internal int AllocatedIndexLength;
    }

    public unsafe struct DynamicBufferArrayHashMapIterator<TKey>
        where TKey : unmanaged
    {
        internal TKey Key;
        internal DynamicValueIndexer NextEntryIndex;
        internal DynamicValueIndexer EntryIndex;
    
        /// <summary>
        /// Returns the entry index.
        /// </summary>
        /// <returns>The entry index.</returns>
        public DynamicValueIndexer GetEntryIndex() => EntryIndex;
    }

    public interface IDynamicBufferArrayHashMap
    {
        
    }
    
    public static class DynamicBufferArrayHashMapExtensions
    {
        public static DynamicBufferArrayHashMap<TKey, TValue> AsArrayHashMap<TBuffer, TKey, TValue>(this DynamicBuffer<TBuffer> buffer)
            where TBuffer : unmanaged, IDynamicBufferArrayHashMap
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return new DynamicBufferArrayHashMap<TKey, TValue>(buffer.Reinterpret<byte>());
        }
    }
}