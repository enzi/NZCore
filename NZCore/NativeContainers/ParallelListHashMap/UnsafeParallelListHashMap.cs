// <copyright project="NZCore" file="UnsafeParallelListHashMap.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NZCore
{
    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct UnsafeParallelListHashMap<TKey, TValue> : IDisposable
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private UnsafeParallelList<TKey> Keys;
        [NativeDisableUnsafePtrRestriction] private UnsafeParallelList<TValue> Values;

        [NativeDisableUnsafePtrRestriction] private UnsafeList<MultipleArrayIndexer>* buckets;
        [NativeDisableUnsafePtrRestriction] private UnsafeList<MultipleArrayIndexer>* next;

        private int keyCapacity;
        private int bucketCapacityMask;
        private int allocatedIndexLength;

        private AllocatorManager.AllocatorHandle allocator;

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static UnsafeParallelListHashMap<TKey, TValue>* Create<TAllocator>(int initialCapacity, ref TAllocator allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
            where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
            UnsafeParallelListHashMap<TKey, TValue>* unsafeArrayHashMap = allocator.Allocate(default(UnsafeParallelListHashMap<TKey, TValue>), 1);

            unsafeArrayHashMap->allocator = allocator.Handle;

            unsafeArrayHashMap->Keys = default;
            unsafeArrayHashMap->Values = default;

            unsafeArrayHashMap->next = UnsafeList<MultipleArrayIndexer>.Create(initialCapacity, allocator.Handle, options);
            unsafeArrayHashMap->buckets = UnsafeList<MultipleArrayIndexer>.Create(initialCapacity * 2, allocator.Handle, options);

            unsafeArrayHashMap->keyCapacity = 0;
            unsafeArrayHashMap->bucketCapacityMask = 0;
            unsafeArrayHashMap->allocatedIndexLength = 0;

            return unsafeArrayHashMap;
        }

        public void SetArrays(UnsafeParallelList<TKey> keyArray, UnsafeParallelList<TValue> valueArray)
        {
            if (!keyArray.IsCreated || !valueArray.IsCreated)
                throw new Exception("Key or values are not created!");
            if (keyArray.Length != valueArray.Length)
                throw new Exception("Key and value length is not the same!");
            if (keyArray.Length == 0 || valueArray.Length == 0)
            {
                allocatedIndexLength = 0;
                return;
            }

            Keys = keyArray;
            Values = valueArray;

            int length = keyArray.Length;
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

            //Debug.Log($"SetArrays with allocatedIndexLength {allocatedIndexLength}");

            CalculateBuckets();
        }

        private void Clear()
        {
            // set all to -1
            UnsafeUtility.MemSet(next->Ptr, 0xFF, keyCapacity * 12); // 4 bytes for 3 ints -> 12
            UnsafeUtility.MemSet(buckets->Ptr, 0xFF, (bucketCapacityMask + 1) * 12);

            next->Clear();
            buckets->Clear();

            allocatedIndexLength = 0;
        }


        private void CalculateBuckets()
        {
            //Debug.Log($"CalculateBuckets with length {allocatedIndexLength} nextCap: {next->Capacity} bucketsCap: {buckets->Capacity}");

            int ii = 0;
            for (int k = 0; k < JobsUtility.ThreadIndexCount; k++)
            {
                var blockCount = Keys.BlockCount(k);
                var keyPtr = (byte*)Keys.GetUnsafeList(k).Ptr;
                var valuePtr = (byte*)Values.GetUnsafeList(k).Ptr;

                for (int i = 0; i < blockCount; i++)
                {
                    var bucketIndex = UnsafeUtility.ReadArrayElement<TKey>(keyPtr, i).GetHashCode() & bucketCapacityMask;

                    (*next)[ii] = (*buckets)[bucketIndex];
                    (*buckets)[bucketIndex] = new MultipleArrayIndexer()
                    {
                        InternalIndex = ii,
                        PtrToKey = keyPtr + i * UnsafeUtility.SizeOf<TKey>(),
                        PtrToValue = valuePtr + i * UnsafeUtility.SizeOf<TValue>()
                    };

                    ii++;
                }
            }
        }

        public bool TryGetFirstRefValue(TKey key, out TValue* item, out UnsafeParallelListHashMapIterator<TKey> it)
        {
            it.Key = key;

            if (allocatedIndexLength <= 0)
            {
                it.EntryIndex = it.NextEntryIndex = MultipleArrayIndexer.Null;
                item = null;
                return false;
            }

            // First find the slot based on the hash            
            int bucket = key.GetHashCode() & bucketCapacityMask;
            it.EntryIndex = it.NextEntryIndex = (*buckets)[bucket];
            return TryGetNextRefValue(out item, ref it);
        }

        public bool TryGetNextRefValue(out TValue* item, ref UnsafeParallelListHashMapIterator<TKey> it)
        {
            var entryIdx = it.NextEntryIndex;
            it.NextEntryIndex = MultipleArrayIndexer.Null;
            it.EntryIndex = MultipleArrayIndexer.Null;
            item = null;

            if (entryIdx.InternalIndex < 0 || entryIdx.InternalIndex >= keyCapacity)
            {
                return false;
            }

            while (!(*(TKey*)entryIdx.PtrToKey).Equals(it.Key))
            {
                entryIdx = (*next)[entryIdx.InternalIndex];
                if (entryIdx.InternalIndex < 0 || entryIdx.InternalIndex >= keyCapacity)
                {
                    return false;
                }
            }

            it.NextEntryIndex = (*next)[entryIdx.InternalIndex];
            it.EntryIndex = entryIdx;

            // Read the value
            item = (TValue*)entryIdx.PtrToValue;

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

        public bool TryPeekNextRefValue(TKey key, MultipleArrayIndexer entryIdx)
        {
            if (entryIdx.InternalIndex < 0 || entryIdx.InternalIndex >= keyCapacity)
            {
                return false;
            }

            while (!(*(TKey*)entryIdx.PtrToKey).Equals(key))
            {
                entryIdx = (*next)[entryIdx.InternalIndex];
                if (entryIdx.InternalIndex < 0 || entryIdx.InternalIndex >= keyCapacity)
                {
                    return false;
                }
            }

            return true;
        }

        public static void Destroy(UnsafeParallelListHashMap<TKey, TValue>* hashMap)
        {
            var allocator = hashMap->allocator;
            hashMap->Dispose();
            AllocatorManager.Free(allocator, hashMap);
        }

        public void Dispose()
        {
            UnsafeList<MultipleArrayIndexer>.Destroy(next, ref allocator);
            UnsafeList<MultipleArrayIndexer>.Destroy(buckets, ref allocator);
        }
    }

    public unsafe struct UnsafeParallelListHashMapEnumerator<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        public UnsafeParallelListHashMap<TKey, TValue>* Map;

        public TKey Key;
        public bool IsFirst;
        private TValue* value;

        private UnsafeParallelListHashMapIterator<TKey> iterator;
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

    public struct UnsafeParallelListHashMapIterator<TKey>
        where TKey : unmanaged
    {
        internal TKey Key;
        internal MultipleArrayIndexer NextEntryIndex;
        internal MultipleArrayIndexer EntryIndex;

        /// <summary>
        /// Returns the entry index.
        /// </summary>
        /// <returns>The entry index.</returns>
        public MultipleArrayIndexer GetEntryIndex() => EntryIndex;
    }

    public unsafe struct MultipleArrayIndexer
    {
        public int InternalIndex;

        public byte* PtrToKey;
        public byte* PtrToValue;

        //public int listIndex;
        //public int elementIndex;

        public static MultipleArrayIndexer Null => new MultipleArrayIndexer()
        {
            InternalIndex = -1,
            PtrToKey = null,
            PtrToValue = null,
            //listIndex = 0,
            //elementIndex = 0,
        };
    }
}