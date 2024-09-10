// <copyright project="NZCore" file="UnsafeKeyDynamicValueArrayHashMap.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NZCore
{
    public interface IKeyInterpreter<in TKey>
    {
        int GetSize(TKey key);
    }

    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct UnsafeKeyDynamicValueArrayHashMap<TKey, TKeyInterpreter> : IDisposable
        where TKey : unmanaged, IEquatable<TKey>
        where TKeyInterpreter : unmanaged, IKeyInterpreter<TKey>
    {
        private int keyCapacity;
        private int bucketCapacityMask;
        private int allocatedIndexLength;

        private TKeyInterpreter keyInterpreter;

        [NativeDisableUnsafePtrRestriction] private TKey* Keys;
        [NativeDisableUnsafePtrRestriction] private byte* Values;

        [NativeDisableUnsafePtrRestriction] private UnsafeList<DynamicValueIndexer>* buckets;
        [NativeDisableUnsafePtrRestriction] private UnsafeList<DynamicValueIndexer>* next;

        private AllocatorManager.AllocatorHandle m_Allocator;

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static UnsafeKeyDynamicValueArrayHashMap<TKey, TKeyInterpreter>* Create<TAllocator>(
            int initialCapacity,
            ref TAllocator allocator,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
            where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
            UnsafeKeyDynamicValueArrayHashMap<TKey, TKeyInterpreter>* unsafeArrayHashMap = allocator.Allocate(default(UnsafeKeyDynamicValueArrayHashMap<TKey, TKeyInterpreter>), 1);

            unsafeArrayHashMap->m_Allocator = allocator.Handle;

            unsafeArrayHashMap->Keys = null;
            unsafeArrayHashMap->Values = null;

            unsafeArrayHashMap->next = UnsafeList<DynamicValueIndexer>.Create(initialCapacity, allocator.Handle, options);
            unsafeArrayHashMap->buckets = UnsafeList<DynamicValueIndexer>.Create(initialCapacity * 2, allocator.Handle, options);

            unsafeArrayHashMap->keyCapacity = 0;
            unsafeArrayHashMap->bucketCapacityMask = 0;
            unsafeArrayHashMap->allocatedIndexLength = 0;
            unsafeArrayHashMap->keyInterpreter = default;

            return unsafeArrayHashMap;
        }

        public void SetArrays(NativeArray<TKey> keyArray, NativeArray<byte> valueArray)
        {
            if (!keyArray.IsCreated || !valueArray.IsCreated)
                throw new Exception("Key or values are not created!");
            if (keyArray.Length != valueArray.Length)
                throw new Exception("Key and value length is not the same!");

            if (valueArray.Length == 0)
            {
                allocatedIndexLength = 0;
                return;
            }

            Keys = (TKey*)keyArray.GetUnsafeReadOnlyPtr();
            Values = (byte*)valueArray.GetUnsafeReadOnlyPtr();

            int length = keyArray.Length;
            int bucketLength = length * 2;

            keyCapacity = length;
            bucketLength = math.ceilpow2(bucketLength);
            bucketCapacityMask = bucketLength - 1;

            //Debug.Log($"Set next/buckets cap to {length}/{bucketLength}");
            next->Resize(length, NativeArrayOptions.UninitializedMemory);
            buckets->Resize(bucketLength, NativeArrayOptions.UninitializedMemory);

            UnsafeUtility.MemSet(next->Ptr, 0xFF, length * 8); // sets everything to max, Unity uses the same method in their NativeHashMap clear
            UnsafeUtility.MemSet(buckets->Ptr, 0xFF, bucketLength * 8);

            allocatedIndexLength = length;

            //Debug.Log($"SetArrays with allocatedIndexLength {allocatedIndexLength}");
        }

        private void Clear()
        {
            // set all to -1
            UnsafeUtility.MemSet(next->Ptr, 0xff, (keyCapacity) * 8);
            UnsafeUtility.MemSet(buckets->Ptr, 0xff, (bucketCapacityMask + 1) * 8);

            next->m_length = 0;
            buckets->m_length = 0;

            allocatedIndexLength = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculateBuckets()
        {
            //Debug.Log($"CalculateBuckets with length {allocatedIndexLength} nextCap: {next->Capacity} bucketsCap: {buckets->Capacity}");

            int valueIndex = 0;
            for (int i = 0; i < allocatedIndexLength; i++)
            {
                var key = Keys[i];
                var bucketIndex = key.GetHashCode() & bucketCapacityMask;

                (*next)[i] = (*buckets)[bucketIndex];
                (*buckets)[bucketIndex] = new DynamicValueIndexer()
                {
                    Index = i,
                    ValueIndex = valueIndex
                };

                valueIndex += keyInterpreter.GetSize(key);
            }
        }

        public bool TryGetFirstRefValue(TKey key, out byte* item, out KeyDynamicValueArrayHashMapIterator<TKey> it)
        {
            it.Key = key;

            if (allocatedIndexLength <= 0)
            {
                it.EntryIndex = it.NextEntryIndex = DynamicValueIndexer.Null;
                item = null;
                return false;
            }

            // First find the slot based on the hash            
            int bucket = key.GetHashCode() & bucketCapacityMask;
            it.EntryIndex = it.NextEntryIndex = (*buckets)[bucket];
            return TryGetNextRefValue(out item, ref it);
        }

        public bool TryGetNextRefValue(out byte* item, ref KeyDynamicValueArrayHashMapIterator<TKey> it)
        {
            var entryIdx = it.NextEntryIndex;
            it.NextEntryIndex = DynamicValueIndexer.Null;
            it.EntryIndex = DynamicValueIndexer.Null;
            item = null;

            if (entryIdx.Index < 0 || entryIdx.Index >= keyCapacity)
            {
                return false;
            }

            while (!Keys[entryIdx.Index].Equals(it.Key))
            {
                entryIdx = (*next)[entryIdx.Index];
                if (entryIdx.Index < 0 || entryIdx.Index >= keyCapacity)
                {
                    return false;
                }
            }

            it.NextEntryIndex = (*next)[entryIdx.Index];
            it.EntryIndex = entryIdx;

            // Read the value
            item = Values + entryIdx.ValueIndex;

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

        public bool TryPeekNextRefValue(TKey key, DynamicValueIndexer entryIdx)
        {
            if (entryIdx.Index < 0 || entryIdx.Index >= keyCapacity)
                return false;

            while (!Keys[entryIdx.Index].Equals(key))
            {
                entryIdx = (*next)[entryIdx.Index];

                if (entryIdx.Index < 0 || entryIdx.Index >= keyCapacity)
                    return false;
            }

            return true;
        }

        public static void Destroy(UnsafeKeyDynamicValueArrayHashMap<TKey, TKeyInterpreter>* hashMap)
        {
            var allocator = hashMap->m_Allocator;
            hashMap->Dispose();
            AllocatorManager.Free(allocator, hashMap);
        }

        public void Dispose()
        {
            UnsafeList<DynamicValueIndexer>.Destroy(next, ref m_Allocator);
            UnsafeList<DynamicValueIndexer>.Destroy(buckets, ref m_Allocator);
        }
    }

    public unsafe struct KeyDynamicValueArrayHashMapEnumerator<TKey, TKeyInterpreter>
        where TKey : unmanaged, IEquatable<TKey>
        where TKeyInterpreter : unmanaged, IKeyInterpreter<TKey>
    {
        public UnsafeKeyDynamicValueArrayHashMap<TKey, TKeyInterpreter>* Map;
        public TKey Key;
        public bool IsFirst;

        private KeyDynamicValueArrayHashMapIterator<TKey> iterator;
        private byte* value;

        public byte* Current => value;
        //public ref TValue Current => ref UnsafeUtility.AsRef<TValue>(value);

        public bool MoveNext()
        {
            //Avoids going beyond the end of the collection.
            if (!IsFirst)
                return Map->TryGetNextRefValue(out value, ref iterator);

            IsFirst = false;
            return Map->TryGetFirstRefValue(Key, out value, out iterator);
        }
    }

    public struct KeyDynamicValueArrayHashMapIterator<TKey>
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

    public struct DynamicValueIndexer
    {
        public int Index;
        public int ValueIndex;

        public static DynamicValueIndexer Null => new DynamicValueIndexer()
        {
            Index = -1,
            ValueIndex = -1
        };
    }
}