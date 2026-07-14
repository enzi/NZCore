// <copyright project="NZCore" file="UnsafeArrayHashMap.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NZCore
{
    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct UnsafeArrayHashMap<TKey, TValue> : IDisposable
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] private byte* values;
        [NoAlias] [NativeDisableUnsafePtrRestriction] private int* buckets;
        [NoAlias] [NativeDisableUnsafePtrRestriction] private int* next;

        [NativeDisableUnsafePtrRestriction] private UnsafeList<int>* bucketsAndNextList;

        private int keyCapacity;
        private int bucketCapacityMask;
        private int allocatedIndexLength;
        private int keyOffset;

        private AllocatorManager.AllocatorHandle m_Allocator; // 8 bytes
        // should be 56 bytes

        public int Length => allocatedIndexLength;


        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static UnsafeArrayHashMap<TKey, TValue>* Create<TAllocator>(int keyOffset, ref TAllocator allocator,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
            where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
            var unsafeArrayHashMap =
                (UnsafeArrayHashMap<TKey, TValue>*)allocator.Allocate(UnsafeUtility.SizeOf<UnsafeArrayHashMap<TKey, TValue>>(), JobsUtility.CacheLineSize, 1);

            unsafeArrayHashMap->m_Allocator = allocator.Handle;

            unsafeArrayHashMap->values = null;
            unsafeArrayHashMap->bucketsAndNextList = UnsafeList<int>.Create(0, allocator.Handle, options);

            unsafeArrayHashMap->next = unsafeArrayHashMap->bucketsAndNextList->Ptr;
            unsafeArrayHashMap->buckets = unsafeArrayHashMap->bucketsAndNextList->Ptr;

            unsafeArrayHashMap->keyCapacity = 0;
            unsafeArrayHashMap->bucketCapacityMask = 0;
            unsafeArrayHashMap->allocatedIndexLength = 0;
            unsafeArrayHashMap->keyOffset = keyOffset;

            return unsafeArrayHashMap;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCapacity(int capacity)
        {
            var length = capacity;
            var bucketLength = length * 2;

            keyCapacity = length;
            bucketLength = math.ceilpow2(bucketLength);
            bucketCapacityMask = bucketLength - 1;

            var sizeOfInt = UnsafeUtility.SizeOf<int>();
            var nextSize = CollectionHelper.Align(sizeOfInt * length, JobsUtility.CacheLineSize) / sizeOfInt;
            var bucketSize = CollectionHelper.Align(sizeOfInt * bucketLength, JobsUtility.CacheLineSize) / sizeOfInt;

            //Debug.Log($"Set next/buckets cap to {length}/{bucketLength} to {nextSize}/{bucketSize} - keyOffset: {keyOffset}");

            bucketsAndNextList->Resize(nextSize + bucketSize, NativeArrayOptions.UninitializedMemory);
            next = bucketsAndNextList->Ptr;
            buckets = bucketsAndNextList->Ptr + nextSize;

            // next[] needs no init: chains only ever reference inserted indices, whose next entries are always written
            UnsafeUtility.MemSet(buckets, 0xFF, bucketLength * sizeOfInt); // sets buckets to -1

            allocatedIndexLength = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetArrays([NoAlias] ref NativeArray<TValue> valueArray)
        {
            var length = valueArray.Length;

            if (length == 0)
            {
                Clear();
                return;
            }

            SetCapacity(length);

            values = (byte*)valueArray.GetUnsafeReadOnlyPtr();
            allocatedIndexLength = length;
        }

        private void Clear()
        {
            // set buckets to -1; next[] needs no init (see SetCapacity)
            UnsafeUtility.MemSet(buckets, 0xFF, (bucketCapacityMask + 1) * 4);

            allocatedIndexLength = 0;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculateBuckets()
        {
            var keyArrayPtr = values + keyOffset;
            var size = sizeof(TValue);

            var localBuckets = buckets;
            var localNext = next;
            var mask = bucketCapacityMask;
            var length = allocatedIndexLength;

            for (var i = 0; i < length; i++)
            {
                var bucketIndex = (*(TKey*)keyArrayPtr).GetHashCode() & mask;

                localNext[i] = localBuckets[bucketIndex];
                localBuckets[bucketIndex] = i;

                keyArrayPtr += size;
            }
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculateBuckets(int oldLength, int length)
        {
            //Debug.Log($"CalculateBuckets with length {allocatedIndexLength} nextCap: {next->Capacity} bucketsCap: {buckets->Capacity}");

            var keyArrayPtr = values + keyOffset;
            var size = sizeof(TValue);

            var localBuckets = buckets;
            var mask = bucketCapacityMask;
            var nextPtrs = next + oldLength;

            for (var i = 0; i < length; i++)
            {
                var bucketIndex = (*(TKey*)keyArrayPtr).GetHashCode() & mask;

                nextPtrs[i] = localBuckets[bucketIndex];
                localBuckets[bucketIndex] = oldLength + i;

                keyArrayPtr += size;
            }

            allocatedIndexLength = oldLength + length;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculateBucketsSingle(TValue* valueArray, int length)
        {
            if (length == 0)
            {
                return;
            }

            //Debug.Log($"CalculateBuckets with length {allocatedIndexLength} length: {length}");

            var oldLength = allocatedIndexLength;
            allocatedIndexLength += length;

            var keyArrayPtr = (byte*)valueArray + keyOffset;
            var size = sizeof(TValue);

            var localBuckets = buckets;
            var mask = bucketCapacityMask;
            var nextPtrs = next + oldLength;

            for (var i = 0; i < length; i++)
            {
                var key = *(TKey*)keyArrayPtr;
                var bucketIndex = key.GetHashCode() & mask;

                nextPtrs[i] = localBuckets[bucketIndex];
                localBuckets[bucketIndex] = oldLength + i;

                keyArrayPtr += size;
            }
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculateBucketsParallel(TValue* valueArray, int length)
        {
            //Debug.Log($"CalculateBuckets with length {allocatedIndexLength} nextCap: {next->Capacity} bucketsCap: {buckets->Capacity}");

            // leave it here as learning experience. another thread could change allocatedIndexLength so it's not safe to read here and write to oldLength
            //var oldLength = allocatedIndexLength;
            //var newLength = Interlocked.Add(ref allocatedIndexLength, length);

            var newLength = Interlocked.Add(ref allocatedIndexLength, length);
            var oldLength = newLength - length;

            var keyArrayPtr = (byte*)valueArray + keyOffset;
            var size = sizeof(TValue);

            var localBuckets = buckets;
            var mask = bucketCapacityMask;
            var nextPtrs = next + oldLength;

            for (var i = 0; i < length; i++)
            {
                var bucketIndex = (*(TKey*)keyArrayPtr).GetHashCode() & mask;
                var index = oldLength + i;
                var nextIndex = Interlocked.Exchange(ref UnsafeUtility.ArrayElementAsRef<int>(localBuckets, bucketIndex), index);
                nextPtrs[i] = nextIndex;

                keyArrayPtr += size;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TKey* GetKeyArrayPtr() => (TKey*)(values + keyOffset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TKey GetKey(int index) => *(TKey*)(values + index * sizeof(TValue) + keyOffset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TValue GetValue(int index) => *(TValue*)(values + index * sizeof(TValue));

        public void SetValuePtr(byte* newPtr)
        {
            values = newPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetFirstRefValue(TKey key, out byte* item, out ArrayHashMapIterator<TKey> it)
        {
            it.Key = key;

            if (allocatedIndexLength <= 0)
            {
                it.EntryIndex = it.NextEntryIndex = -1;
                item = null;
                return false;
            }

            // First find the slot based on the hash            
            var bucket = key.GetHashCode() & bucketCapacityMask;
            it.EntryIndex = it.NextEntryIndex = buckets[bucket];
            return TryGetNextRefValue(out item, ref it);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetNextRefValue(out byte* item, ref ArrayHashMapIterator<TKey> it)
        {
            var entryIdx = it.NextEntryIndex;
            var localNext = next;
            var cap = (uint)keyCapacity;

            if ((uint)entryIdx >= cap)
            {
                it.NextEntryIndex = -1;
                it.EntryIndex = -1;
                item = null;
                return false;
            }

            while (!GetKey(entryIdx).Equals(it.Key))
            {
                entryIdx = localNext[entryIdx];
                if ((uint)entryIdx >= cap)
                {
                    it.NextEntryIndex = -1;
                    it.EntryIndex = -1;
                    item = null;
                    return false;
                }
            }

            it.NextEntryIndex = localNext[entryIdx];
            it.EntryIndex = entryIdx;
            item = values + entryIdx * sizeof(TValue);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekFirstRefValue(TKey key)
        {
            if (allocatedIndexLength <= 0)
            {
                return false;
            }

            // First find the slot based on the hash            
            var bucket = key.GetHashCode() & bucketCapacityMask;
            return TryPeekNextRefValue(key, buckets[bucket]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekNextRefValue(TKey key, int entryIdx)
        {
            var localNext = next;
            var cap = (uint)keyCapacity;

            if ((uint)entryIdx >= cap)
            {
                return false;
            }

            while (!GetKey(entryIdx).Equals(key))
            {
                entryIdx = localNext[entryIdx];
                if ((uint)entryIdx >= cap)
                {
                    return false;
                }
            }

            return true;
        }

        public void GetKeyValueArrays(NativeKeyValueArrays<TKey, TValue> result)
        {
            for (int i = 0, count = 0, max = result.Length, capacityMask = bucketCapacityMask; i <= capacityMask && count < max; ++i)
            {
                var bucket = buckets[i];

                while (bucket != -1)
                {
                    result.Keys[count] = GetKey(bucket);
                    result.Values[count] = GetValue(bucket);
                    count++;
                    bucket = next[bucket];
                }
            }
        }

        public static void Destroy(UnsafeArrayHashMap<TKey, TValue>* hashMap)
        {
            var allocator = hashMap->m_Allocator;
            hashMap->Dispose();
            AllocatorManager.Free(allocator, hashMap);
        }

        public void Dispose()
        {
            if (bucketsAndNextList->IsCreated)
            {
                UnsafeList<int>.Destroy(bucketsAndNextList, ref m_Allocator);
            }
        }
    }

    public unsafe struct ArrayHashMapEnumerator<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        public bool IsFirst;
        public TKey Key;
        public UnsafeArrayHashMap<TKey, TValue>* Map;
        private ArrayHashMapIterator<TKey> _iterator;

        private byte* _value;

        public ref TValue Current => ref UnsafeUtility.AsRef<TValue>(_value);
        public TValue* CurrentPtr => (TValue*)_value;
        public int CurrentIndex => _iterator.EntryIndex;

        public bool MoveNext()
        {
            //Avoids going beyond the end of the collection.
            if (!IsFirst)
            {
                return Map->TryGetNextRefValue(out _value, ref _iterator);
            }

            IsFirst = false;
            return Map->TryGetFirstRefValue(Key, out _value, out _iterator);
        }
    }

    public struct ArrayHashMapIterator<TKey>
        where TKey : unmanaged
    {
        internal TKey Key;
        internal int NextEntryIndex;
        internal int EntryIndex;

        public int GetEntryIndex() => EntryIndex;
    }
}