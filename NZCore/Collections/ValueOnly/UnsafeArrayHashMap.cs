// <copyright project="NZCore" file="UnsafeArrayHashMap.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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
        [NativeDisableUnsafePtrRestriction] private byte* values;
        [NativeDisableUnsafePtrRestriction] private int* buckets;
        [NativeDisableUnsafePtrRestriction] private int* next;

        [NativeDisableUnsafePtrRestriction] private UnsafeList<int>* bucketsAndNextList;

        private int keyCapacity;
        private int bucketCapacityMask;
        private int allocatedIndexLength;
        private int keyOffset;

        private AllocatorManager.AllocatorHandle m_Allocator; // 8 bytes
        // should be 56 bytes

        public int Length => allocatedIndexLength;


        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static UnsafeArrayHashMap<TKey, TValue>* Create<TAllocator>(int keyOffset, ref TAllocator allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
            where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
            UnsafeArrayHashMap<TKey, TValue>* unsafeArrayHashMap =
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
            int length = capacity;
            int bucketLength = length * 2;

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

            UnsafeUtility.MemSet(bucketsAndNextList->Ptr, 0xFF, (nextSize + bucketSize) * 4); // sets everything to -1

            allocatedIndexLength = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetArrays([NoAlias] ref NativeArray<TValue> valueArray)
        {
            int length = valueArray.Length;

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
            // set all to -1
            UnsafeUtility.MemSet(buckets, 0xFF, (bucketCapacityMask + 1) * 4);
            UnsafeUtility.MemSet(next, 0xFF, (keyCapacity) * 4);

            allocatedIndexLength = 0;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculateBuckets()
        {
            byte* keyArrayPtr = (values + keyOffset);
            var size = sizeof(TValue);

            for (int i = 0; i < allocatedIndexLength; i++)
            {
                var bucketIndex = (*(TKey*)keyArrayPtr).GetHashCode() & bucketCapacityMask;

                next[i] = buckets[bucketIndex];
                buckets[bucketIndex] = i;

                keyArrayPtr += size;
            }
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculateBuckets(int oldLength, int length)
        {
            //Debug.Log($"CalculateBuckets with length {allocatedIndexLength} nextCap: {next->Capacity} bucketsCap: {buckets->Capacity}");

            byte* keyArrayPtr = (values + keyOffset);
            var size = sizeof(TValue);

            int* nextPtrs = next + oldLength;

            for (int i = 0; i < length; i++)
            {
                var bucketIndex = (*(TKey*)keyArrayPtr).GetHashCode() & bucketCapacityMask;

                nextPtrs[i] = buckets[bucketIndex];
                buckets[bucketIndex] = oldLength + i;

                keyArrayPtr += size;
            }

            allocatedIndexLength = oldLength + length;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculateBucketsSingle(TValue* valueArray, int length)
        {
            if (length == 0)
                return;

            //Debug.Log($"CalculateBuckets with length {allocatedIndexLength} length: {length}");

            var oldLength = allocatedIndexLength;
            allocatedIndexLength += length;

            byte* keyArrayPtr = ((byte*)valueArray + keyOffset);
            var size = sizeof(TValue);

            int* nextPtrs = next + oldLength;

            for (int i = 0; i < length; i++)
            {
                var key = *(TKey*)keyArrayPtr;
                var bucketIndex = key.GetHashCode() & bucketCapacityMask;

                nextPtrs[i] = buckets[bucketIndex];
                buckets[bucketIndex] = oldLength + i;

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

            byte* keyArrayPtr = ((byte*)valueArray + keyOffset);
            var size = sizeof(TValue);

            var nextPtrs = next + oldLength;

            for (int i = 0; i < length; i++)
            {
                var bucketIndex = (*(TKey*)keyArrayPtr).GetHashCode() & bucketCapacityMask;
                var index = oldLength + i;
                var nextIndex = Interlocked.Exchange(ref UnsafeUtility.ArrayElementAsRef<int>(buckets, bucketIndex), index);
                nextPtrs[i] = nextIndex;

                keyArrayPtr += size;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TKey* GetKeyArrayPtr()
        {
            return (TKey*)(values + keyOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TKey GetKey(int index)
        {
            return *(TKey*)(values + index * sizeof(TValue) + keyOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TValue GetValue(int index)
        {
            return *(TValue*)(values + index * sizeof(TValue));
        }

        public void SetValuePtr(byte* newPtr)
        {
            values = newPtr;
        }

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
            int bucket = key.GetHashCode() & bucketCapacityMask;
            it.EntryIndex = it.NextEntryIndex = buckets[bucket];
            return TryGetNextRefValue(out item, ref it);
        }

        public bool TryGetNextRefValue(out byte* item, ref ArrayHashMapIterator<TKey> it)
        {
            int entryIdx = it.NextEntryIndex;

            if (entryIdx < 0 || entryIdx >= keyCapacity)
            {
                it.NextEntryIndex = -1;
                it.EntryIndex = -1;
                item = null;
                return false;
            }

            while (!GetKey(entryIdx).Equals(it.Key))
            {
                entryIdx = next[entryIdx];
                if (entryIdx < 0 || entryIdx >= keyCapacity)
                {
                    it.NextEntryIndex = -1;
                    it.EntryIndex = -1;
                    item = null;
                    return false;
                }
            }

            it.NextEntryIndex = next[entryIdx];
            it.EntryIndex = entryIdx;
            item = values + entryIdx * sizeof(TValue);

            return true;
        }

        public bool TryPeekFirstRefValue(TKey key)
        {
            if (allocatedIndexLength <= 0)
                return false;

            // First find the slot based on the hash            
            int bucket = key.GetHashCode() & bucketCapacityMask;
            return TryPeekNextRefValue(key, buckets[bucket]);
        }

        public bool TryPeekNextRefValue(TKey key, int entryIdx)
        {
            if (entryIdx < 0 || entryIdx >= keyCapacity)
            {
                return false;
            }

            while (!GetKey(entryIdx).Equals(key))
            {
                entryIdx = next[entryIdx];
                if (entryIdx < 0 || entryIdx >= keyCapacity)
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
                int bucket = buckets[i];

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
                UnsafeList<int>.Destroy(bucketsAndNextList, ref m_Allocator);
        }
    }

    public unsafe struct ArrayHashMapEnumerator<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        public bool IsFirst;
        public TKey Key;
        public UnsafeArrayHashMap<TKey, TValue>* Map;
        private ArrayHashMapIterator<TKey> iterator;

        private byte* value;

        public ref TValue Current => ref UnsafeUtility.AsRef<TValue>(value);
        public TValue* CurrentPtr => (TValue*)value;
        public int CurrentIndex => iterator.EntryIndex;

        public bool MoveNext()
        {
            //Avoids going beyond the end of the collection.
            if (!IsFirst)
                return Map->TryGetNextRefValue(out value, ref iterator);

            IsFirst = false;
            return Map->TryGetFirstRefValue(Key, out value, out iterator);
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