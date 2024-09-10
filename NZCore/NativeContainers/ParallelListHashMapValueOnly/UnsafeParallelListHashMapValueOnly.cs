// <copyright project="NZCore" file="UnsafeParallelListHashMapValueOnly.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NZCore
{
    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct UnsafeParallelListHashMapValueOnly<TKey, TValue> : IDisposable
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private UnsafeParallelList<TValue> Values;

        [NativeDisableUnsafePtrRestriction] private UnsafeList<MultipleArrayIndexerNoPointer>* buckets;
        [NativeDisableUnsafePtrRestriction] private UnsafeList<MultipleArrayIndexerNoPointer>* next;

        private int keyCapacity;
        private int bucketCapacityMask;
        private int allocatedIndexLength;
        private int keyOffset;

        private AllocatorManager.AllocatorHandle allocator;


        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static UnsafeParallelListHashMapValueOnly<TKey, TValue>* Create<TAllocator>(int initialCapacity, int keyOffset, ref TAllocator allocator,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
            where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
            UnsafeParallelListHashMapValueOnly<TKey, TValue>* unsafeArrayHashMap = allocator.Allocate(default(UnsafeParallelListHashMapValueOnly<TKey, TValue>), 1);

            unsafeArrayHashMap->allocator = allocator.Handle;

            unsafeArrayHashMap->Values = default;

            unsafeArrayHashMap->next = UnsafeList<MultipleArrayIndexerNoPointer>.Create(initialCapacity, allocator.Handle, options);
            unsafeArrayHashMap->buckets = UnsafeList<MultipleArrayIndexerNoPointer>.Create(initialCapacity * 2, allocator.Handle, options);

            unsafeArrayHashMap->keyCapacity = 0;
            unsafeArrayHashMap->bucketCapacityMask = 0;
            unsafeArrayHashMap->allocatedIndexLength = 0;
            unsafeArrayHashMap->keyOffset = keyOffset;

            return unsafeArrayHashMap;
        }

        public void SetArrays(UnsafeParallelList<TValue> valueArray)
        {
            Values = valueArray;

            int length = valueArray.Length;
            int bucketLength = length * 2;

            keyCapacity = length;
            bucketLength = math.ceilpow2(bucketLength);
            bucketCapacityMask = bucketLength - 1;

            //Debug.Log($"Set next/buckets cap to {length}/{bucketLength}");
            next->Resize(length, NativeArrayOptions.UninitializedMemory);
            buckets->Resize(bucketLength, NativeArrayOptions.UninitializedMemory);

            UnsafeUtility.MemSet(next->Ptr, 0xFF, length * 12); // 4 bytes for 3 ints -> 12
            UnsafeUtility.MemSet(buckets->Ptr, 0xFF, bucketLength * 12);

            allocatedIndexLength = length;
        }

        private void Clear()
        {
            // set all to -1
            UnsafeUtility.MemSet(next->Ptr, 0xFF, (keyCapacity) * 12); // 4 bytes for 3 ints -> 12
            UnsafeUtility.MemSet(buckets->Ptr, 0xFF, (bucketCapacityMask + 1) * 12);
            next->Clear();
            buckets->Clear();

            allocatedIndexLength = 0;
        }


        public void CalculateBuckets()
        {
            //Debug.Log($"CalculateBuckets with length {allocatedIndexLength} nextCap: {next->Capacity} bucketsCap: {buckets->Capacity}");

            var size = sizeof(TValue);

            int ii = 0;
            for (int k = 0; k < JobsUtility.ThreadIndexCount; k++)
            {
                ref var list = ref Values.GetUnsafeList(k);
                var blockCount = list.Length;

                byte* keyArrayPtr = ((byte*)list.Ptr) + keyOffset;

                for (int i = 0; i < blockCount; i++)
                {
                    var bucketIndex = (*(TKey*)keyArrayPtr).GetHashCode() & bucketCapacityMask;

                    (*next)[ii] = (*buckets)[bucketIndex];
                    (*buckets)[bucketIndex] = new MultipleArrayIndexerNoPointer()
                    {
                        ListIndex = k,
                        ElementIndex = i,
                        BucketIndex = ii
                    };

                    ii++;
                    keyArrayPtr += size;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TKey* GetKeyArrayPtr(int k)
        {
            return (TKey*)((byte*)Values.GetUnsafeList(k).Ptr + keyOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TKey GetKey(int k, int index)
        {
            return *(TKey*)(((byte*)Values.GetUnsafeList(k).Ptr) + index * sizeof(TValue) + keyOffset);
        }

        public bool TryGetFirstRefValue(TKey key, out byte* item, out UnsafeParallelListHashMapValueOnlyIterator<TKey> it)
        {
            it.Key = key;

            if (allocatedIndexLength <= 0)
            {
                it.EntryIndex = it.NextEntryIndex = MultipleArrayIndexerNoPointer.Null;
                item = null;
                return false;
            }

            // First find the slot based on the hash            
            int bucket = key.GetHashCode() & bucketCapacityMask;
            it.EntryIndex = it.NextEntryIndex = (*buckets)[bucket];
            return TryGetNextRefValue(out item, ref it);
        }

        public bool TryGetNextRefValue(out byte* item, ref UnsafeParallelListHashMapValueOnlyIterator<TKey> it)
        {
            MultipleArrayIndexerNoPointer entryIdx = it.NextEntryIndex;
            it.NextEntryIndex = MultipleArrayIndexerNoPointer.Null;
            it.EntryIndex = MultipleArrayIndexerNoPointer.Null;
            item = null;

            if (entryIdx.ListIndex < 0 || entryIdx.ElementIndex < 0)
            {
                return false;
            }

            while (!GetKey(entryIdx.ListIndex, entryIdx.ElementIndex).Equals(it.Key))
            {
                entryIdx = (*next)[entryIdx.BucketIndex];
                if (entryIdx.BucketIndex < 0 || entryIdx.BucketIndex >= keyCapacity)
                {
                    return false;
                }
            }

            it.NextEntryIndex = (*next)[entryIdx.BucketIndex];
            it.EntryIndex = entryIdx;

            // Read the value
            item = Values.GetUnsafePtr(entryIdx.ListIndex) + entryIdx.ElementIndex * sizeof(TValue);

            return true;
        }

        public bool TryPeekFirstRefValue(TKey key)
        {
            if (allocatedIndexLength <= 0)
                return false;

            // First find the slot based on the hash            
            int bucket = key.GetHashCode() & bucketCapacityMask;
            return TryPeekNextRefValue(key, (*buckets)[bucket]);
        }

        public bool TryPeekNextRefValue(TKey key, MultipleArrayIndexerNoPointer entryIdx)
        {
            if (entryIdx.ListIndex < 0 || entryIdx.ElementIndex < 0)
                return false;

            while (!GetKey(entryIdx.ListIndex, entryIdx.ElementIndex).Equals(key))
            {
                entryIdx = (*next)[entryIdx.BucketIndex];
                if (entryIdx.BucketIndex < 0 || entryIdx.BucketIndex >= keyCapacity)
                {
                    return false;
                }
            }

            return true;
        }

        public static void Destroy(UnsafeParallelListHashMapValueOnly<TKey, TValue>* hashMap)
        {
            var allocator = hashMap->allocator;
            hashMap->Dispose();
            AllocatorManager.Free(allocator, hashMap);
        }

        public void Dispose()
        {
            UnsafeList<MultipleArrayIndexerNoPointer>.Destroy(next, ref allocator);
            UnsafeList<MultipleArrayIndexerNoPointer>.Destroy(buckets, ref allocator);
        }
    }

    public unsafe struct UnsafeParallelListHashMapValueOnlyEnumerator<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        public UnsafeParallelListHashMapValueOnly<TKey, TValue>* Map;

        public TKey Key;
        public bool IsFirst;
        private byte* value;

        private UnsafeParallelListHashMapValueOnlyIterator<TKey> iterator;
        public ref TValue Current => ref UnsafeUtility.AsRef<TValue>(value);

        public bool MoveNext()
        {
            //Avoids going beyond the end of the collection.
            if (!IsFirst)
                return Map->TryGetNextRefValue(out value, ref iterator);

            IsFirst = false;
            return Map->TryGetFirstRefValue(Key, out value, out iterator);
        }
    }

    public struct UnsafeParallelListHashMapValueOnlyIterator<TKey>
        where TKey : unmanaged
    {
        internal TKey Key;
        internal MultipleArrayIndexerNoPointer NextEntryIndex;
        internal MultipleArrayIndexerNoPointer EntryIndex;

        /// <summary>
        /// Returns the entry index.
        /// </summary>
        /// <returns>The entry index.</returns>
        public MultipleArrayIndexerNoPointer GetEntryIndex() => EntryIndex;
    }

    public struct MultipleArrayIndexerNoPointer
    {
        public int ListIndex;
        public int ElementIndex;
        public int BucketIndex;

        public static MultipleArrayIndexerNoPointer Null => new MultipleArrayIndexerNoPointer()
        {
            ListIndex = -1,
            ElementIndex = -1,
            BucketIndex = -1
        };
    }
}