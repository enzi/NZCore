// <copyright project="NZCore" file="UnsafeKeyValueArrayHashMap.cs" version="0.1">
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
    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct UnsafeKeyValueArrayHashMap<TKey, TValue> : IDisposable
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        private int keyCapacity;
        private int bucketCapacityMask;
        private int allocatedIndexLength;
        private int keyOffset;

        [NativeDisableUnsafePtrRestriction] private TKey* Keys;
        [NativeDisableUnsafePtrRestriction] private TValue* Values;

        [NativeDisableUnsafePtrRestriction] private UnsafeList<int>* buckets;
        [NativeDisableUnsafePtrRestriction] private UnsafeList<int>* next;

        private AllocatorManager.AllocatorHandle m_Allocator;

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static UnsafeKeyValueArrayHashMap<TKey, TValue>* Create<TAllocator>(int initialCapacity, int keyOffset, ref TAllocator allocator,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
            where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
            UnsafeKeyValueArrayHashMap<TKey, TValue>* unsafeArrayHashMap = allocator.Allocate(default(UnsafeKeyValueArrayHashMap<TKey, TValue>), 1);

            unsafeArrayHashMap->m_Allocator = allocator.Handle;

            unsafeArrayHashMap->Keys = null;
            unsafeArrayHashMap->Values = null;

            unsafeArrayHashMap->next = UnsafeList<int>.Create(initialCapacity, allocator.Handle, options);
            unsafeArrayHashMap->buckets = UnsafeList<int>.Create(initialCapacity * 2, allocator.Handle, options);

            unsafeArrayHashMap->keyCapacity = 0;
            unsafeArrayHashMap->bucketCapacityMask = 0;
            unsafeArrayHashMap->allocatedIndexLength = 0;
            unsafeArrayHashMap->keyOffset = keyOffset;

            return unsafeArrayHashMap;
        }

        public void SetArrays(NativeArray<TKey> keyArray, NativeArray<TValue> valueArray)
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
            Values = (TValue*)valueArray.GetUnsafeReadOnlyPtr();

            int length = valueArray.Length;
            int bucketLength = length * 2;

            keyCapacity = length;
            bucketLength = math.ceilpow2(bucketLength);
            bucketCapacityMask = bucketLength - 1;

            //Debug.Log($"Set next/buckets cap to {length}/{bucketLength}");
            next->Resize(length, NativeArrayOptions.UninitializedMemory);
            buckets->Resize(bucketLength, NativeArrayOptions.UninitializedMemory);

            UnsafeUtility.MemSet(next->Ptr, 0xFF, length * 4); // sets everything to max, Unity uses the same method in their NativeHashMap clear
            UnsafeUtility.MemSet(buckets->Ptr, 0xFF, bucketLength * 4);

            allocatedIndexLength = length;

            //Debug.Log($"SetArrays with allocatedIndexLength {allocatedIndexLength}");
        }

        private void Clear()
        {
            // set all to -1
            UnsafeUtility.MemSet(next->Ptr, 0xff, (keyCapacity) * 4);
            UnsafeUtility.MemSet(buckets->Ptr, 0xff, (bucketCapacityMask + 1) * 4);

            next->m_length = 0;
            buckets->m_length = 0;

            allocatedIndexLength = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculateBuckets()
        {
            //Debug.Log($"CalculateBuckets with length {allocatedIndexLength} nextCap: {next->Capacity} bucketsCap: {buckets->Capacity}");

            for (int i = 0; i < allocatedIndexLength; i++)
            {
                var bucketIndex = Keys[i].GetHashCode() & bucketCapacityMask;

                (*next)[i] = (*buckets)[bucketIndex];
                (*buckets)[bucketIndex] = i;
            }
        }

        public bool TryGetFirstRefValue(TKey key, out TValue* item, out KeyValueArrayHashMapIterator<TKey> it)
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
            it.EntryIndex = it.NextEntryIndex = (*buckets)[bucket];
            return TryGetNextRefValue(out item, ref it);
        }

        public bool TryGetNextRefValue(out TValue* item, ref KeyValueArrayHashMapIterator<TKey> it)
        {
            int entryIdx = it.NextEntryIndex;
            it.NextEntryIndex = -1;
            it.EntryIndex = -1;
            item = null;

            if (entryIdx < 0 || entryIdx >= keyCapacity)
            {
                return false;
            }

            while (!Keys[entryIdx].Equals(it.Key))
            {
                entryIdx = (*next)[entryIdx];
                if (entryIdx < 0 || entryIdx >= keyCapacity)
                {
                    return false;
                }
            }

            it.NextEntryIndex = (*next)[entryIdx];
            it.EntryIndex = entryIdx;

            // Read the value
            item = Values + entryIdx;

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

        public bool TryPeekNextRefValue(TKey key, int entryIdx)
        {
            if (entryIdx < 0 || entryIdx >= keyCapacity)
                return false;

            while (!Keys[entryIdx].Equals(key))
            {
                entryIdx = (*next)[entryIdx];

                if (entryIdx < 0 || entryIdx >= keyCapacity)
                    return false;
            }

            return true;
        }

        public static void Destroy(UnsafeKeyValueArrayHashMap<TKey, TValue>* hashMap)
        {
            var allocator = hashMap->m_Allocator;
            hashMap->Dispose();
            AllocatorManager.Free(allocator, hashMap);
        }

        public void Dispose()
        {
            UnsafeList<int>.Destroy(next, ref m_Allocator);
            UnsafeList<int>.Destroy(buckets, ref m_Allocator);
        }
    }

    public unsafe struct KeyValueArrayHashMapEnumerator<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        public UnsafeKeyValueArrayHashMap<TKey, TValue>* Map;
        public TKey Key;
        public bool IsFirst;

        private KeyValueArrayHashMapIterator<TKey> iterator;
        private TValue* value;

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

    public struct KeyValueArrayHashMapIterator<TKey>
        where TKey : unmanaged
    {
        internal TKey Key;
        internal int NextEntryIndex;
        internal int EntryIndex;

        /// <summary>
        /// Returns the entry index.
        /// </summary>
        /// <returns>The entry index.</returns>
        public int GetEntryIndex() => EntryIndex;
    }
}