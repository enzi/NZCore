using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace NZCore
{
    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct UnsafeArrayHashMap<TKey, TValue> : IDisposable
        where TKey : unmanaged, IEquatable<TKey> 
        where TValue : unmanaged
    {
        private int keyCapacity;
        private int bucketCapacityMask;
        private int allocatedIndexLength;
        private int keyOffset;
        
        //[NativeDisableUnsafePtrRestriction] internal byte* Keys;
        [NativeDisableUnsafePtrRestriction] private byte* Values;

        [NativeDisableUnsafePtrRestriction] private UnsafeList<int>* buckets;
        [NativeDisableUnsafePtrRestriction] private UnsafeList<int>* next;

        internal AllocatorManager.AllocatorHandle m_Allocator;

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static UnsafeArrayHashMap<TKey, TValue>* Create<U>(int initialCapacity, int keyOffset, ref U allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
            where U : unmanaged, AllocatorManager.IAllocator
        {
            UnsafeArrayHashMap<TKey, TValue>* unsafeArrayHashMap = allocator.Allocate(default(UnsafeArrayHashMap<TKey, TValue>), 1);

            unsafeArrayHashMap->m_Allocator = allocator.Handle;
            
            //unsafeArrayHashMap->Keys = null;
            unsafeArrayHashMap->Values = null;
            
            unsafeArrayHashMap->next = UnsafeList<int>.Create(initialCapacity, allocator.Handle, options);
            unsafeArrayHashMap->buckets = UnsafeList<int>.Create(initialCapacity * 2, allocator.Handle, options);

            unsafeArrayHashMap->keyCapacity = 0;
            unsafeArrayHashMap->bucketCapacityMask = 0;
            unsafeArrayHashMap->allocatedIndexLength = 0;
            unsafeArrayHashMap->keyOffset = keyOffset;
            
            return unsafeArrayHashMap;
        }

        public void SetArrays(ref NativeArray<TValue> valueArray)
        {
            //if (!keyArray.IsCreated || !valueArray.IsCreated)
            //    throw new Exception("Key or values are not created!");
            //if (keyArray.Length != valueArray.Length)
            //   throw new Exception("Key and value length is not the same!");
            if (valueArray.Length == 0)
            {
                allocatedIndexLength = 0;
                return;
            }
            
            //Keys = (byte*) keyArray.GetUnsafeReadOnlyPtr();
            Values = (byte*) valueArray.GetUnsafeReadOnlyPtr();

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
            
            CalculateBuckets();
        }

        private void Clear()
        {
            // set all to -1
            UnsafeUtility.MemSet(buckets->Ptr, 0xff, (bucketCapacityMask + 1) * 4);
            UnsafeUtility.MemSet(next->Ptr, 0xff, (keyCapacity) * 4);
            next->m_length = 0;
            buckets->m_length = 0;
            
            allocatedIndexLength = 0;
        }

        //[MethodImpl(MethodImplOptions.NoInlining)]
        private void CalculateBuckets()
        {
            //Debug.Log($"CalculateBuckets with length {allocatedIndexLength} nextCap: {next->Capacity} bucketsCap: {buckets->Capacity}");

            //TValue* keyArrayPtr = (TValue*) (Values + keyOffset);
            TKey* keyArrayPtr = (TKey*) (Values + keyOffset);
            
            for (int i = 0; i < allocatedIndexLength; i++)
            {
                //var bucketIndex = GetKey(i).GetHashCode() & bucketCapacityMask;
                //var bucketIndex = UnsafeUtility.As<TValue, TKey>(ref keyArrayPtr[i]).GetHashCode() & bucketCapacityMask;
                var bucketIndex = keyArrayPtr[i].GetHashCode() & bucketCapacityMask;
                
                (*next)[i] = (*buckets)[bucketIndex];
                (*buckets)[bucketIndex] = i;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TKey GetKey(int index)
        {
            return *(TKey*) (Values + index * sizeof(TValue) + keyOffset);
        }

        public bool TryGetFirstRefValue(TKey key, out byte* item, out ArrayHashMapIterator<TKey> it)            
        {
            it.key = key;
            
            if (allocatedIndexLength <= 0)
            {
                it.EntryIndex = it.NextEntryIndex = -1;
                item = null;
                //it.keyPtr = null;
                return false;
            }

            //it.keyPtr = (TValue*) Values + keyOffset;

            // First find the slot based on the hash            
            int bucket = key.GetHashCode() & bucketCapacityMask;
            it.EntryIndex = it.NextEntryIndex = (*buckets)[bucket];
            return TryGetNextRefValue(out item, ref it);
        }

        public bool TryGetNextRefValue(out byte* item, ref ArrayHashMapIterator<TKey> it)           
        {
            int entryIdx = it.NextEntryIndex;
            it.NextEntryIndex = -1;
            it.EntryIndex = -1;
            item = null;
           
            if (entryIdx < 0 || entryIdx >= keyCapacity)
            {
                return false;
            }
            
            
            //while (!(*(TKey*) (Keys + entryIdx * sizeof(TKey))).Equals(it.key))
            while (!GetKey(entryIdx).Equals(it.key))
            //while (UnsafeUtility.As<TValue, TKey>(ref it.keyPtr[entryIdx]).Equals(it.key))
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
            item = Values + entryIdx * sizeof(TValue);

            return true;
        }
        
        public static void Destroy(UnsafeArrayHashMap<TKey, TValue>* hashMap)
        {
            //CheckNull(listData);
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
    
    public unsafe struct ArrayHashMapEnumerator<TKey, TValue> 
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        public TKey key;
        public UnsafeArrayHashMap<TKey, TValue>* Map;
        private ArrayHashMapIterator<TKey> iterator;
        
        private byte* value;
        public bool isFirst;

        public ref TValue Current => ref UnsafeUtility.AsRef<TValue>(value);

        public bool MoveNext()
        {
            //Avoids going beyond the end of the collection.
            if (!isFirst) 
                return Map->TryGetNextRefValue(out value, ref iterator);
            
            isFirst = false;
            return Map->TryGetFirstRefValue(key, out value, out iterator);

        }
    }
        
    public unsafe struct ArrayHashMapIterator<TKey>
        where TKey : unmanaged
        //where TValue : unmanaged
    {
        internal TKey key;
        internal int NextEntryIndex;
        internal int EntryIndex;
        //internal TValue* keyPtr;

        /// <summary>
        /// Returns the entry index.
        /// </summary>
        /// <returns>The entry index.</returns>
        public int GetEntryIndex() => EntryIndex;
    }
}