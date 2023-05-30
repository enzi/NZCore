using System;
using System.Runtime.InteropServices;
using System.Xml;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace NZCore
{
    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct UnsafeParallelListHashMap<TKey, TValue> : IDisposable
        where TKey : unmanaged, IEquatable<TKey> 
        where TValue : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] internal UnsafeParallelList<TKey> Keys;
        [NativeDisableUnsafePtrRestriction] internal UnsafeParallelList<TValue> Values;

        [NativeDisableUnsafePtrRestriction] internal UnsafeList<MultipleArrayIndexer>* buckets;
        [NativeDisableUnsafePtrRestriction] internal UnsafeList<MultipleArrayIndexer>* next;
        
        internal int keyCapacity;
        internal int bucketCapacityMask;
        internal int allocatedIndexLength;

        internal AllocatorManager.AllocatorHandle m_Allocator;


        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static UnsafeParallelListHashMap<TKey, TValue>* Create<U>(int initialCapacity, ref U allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
            where U : unmanaged, AllocatorManager.IAllocator
        {
            UnsafeParallelListHashMap<TKey, TValue>* unsafeArrayHashMap = allocator.Allocate(default(UnsafeParallelListHashMap<TKey, TValue>), 1);

            unsafeArrayHashMap->m_Allocator = allocator.Handle;
            
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

            for (int i = 0; i < length; i++)
                (*next)[i] = MultipleArrayIndexer.Null;

            for (int i = 0; i < bucketLength; i++)
                (*buckets)[i] = MultipleArrayIndexer.Null;
            
            allocatedIndexLength = length;
            
            //Debug.Log($"SetArrays with allocatedIndexLength {allocatedIndexLength}");
            
            CalculateBuckets();
        }

        private void Clear()
        {
            // set all to -1
            UnsafeUtility.MemSet(buckets->Ptr, 0xff, (bucketCapacityMask + 1) * 4);
            UnsafeUtility.MemSet(next->Ptr, 0xff, (keyCapacity) * 4);
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
                ref var threadList = ref Keys.GetUnsafeList(k);
                var ptr = (byte*) threadList.Ptr;
                var valuePtr = (byte*) Values.GetUnsafeList(k).Ptr;
                
                for (int i = 0; i < blockCount; i++)
                {
                    //var bucketIndex = (*(TKey*) (Keys + i * sizeof(TKey))).GetHashCode() & bucketCapacityMask;
                    var bucketIndex = UnsafeUtility.ReadArrayElement<TKey>(ptr, i).GetHashCode() & bucketCapacityMask;
                    

                    (*next)[ii] = (*buckets)[bucketIndex];
                    (*buckets)[bucketIndex] = new MultipleArrayIndexer()
                    {
                        internalIndex = ii,
                        ptrToKey = ptr + i * UnsafeUtility.SizeOf<TKey>(),
                        ptrToValue = valuePtr + i * UnsafeUtility.SizeOf<TValue>()
                        //listIndex = k,
                        //elementIndex = i
                    };

                    ii++;
                }
            }
        }

        public bool TryGetFirstRefValue(TKey key, out byte* item, out UnsafeParallelListHashMapIterator<TKey, TValue> it)            
        {
            it.key = key;            

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

        public bool TryGetNextRefValue(out byte* item, ref UnsafeParallelListHashMapIterator<TKey, TValue> it)           
        {
            var entryIdx = it.NextEntryIndex;
            it.NextEntryIndex = MultipleArrayIndexer.Null;
            it.EntryIndex = MultipleArrayIndexer.Null;
            item = null;
           
            if (entryIdx.internalIndex < 0 || entryIdx.internalIndex >= keyCapacity)
            {
                return false;
            }
            
            //while (!Keys[entryIdx].Equals(it.key))
            //while (!UnsafeUtility.ReadArrayElement<TKey>(Keys, entryIdx).Equals(it.key))
            //while (!Keys.GetUnsafeList(entryIdx.listIndex)[entryIdx.elementIndex].Equals(it.key))
            
            
            while (!(*(TKey*) entryIdx.ptrToKey).Equals(it.key))
            {
                entryIdx = (*next)[entryIdx.internalIndex];
                if (entryIdx.internalIndex < 0 || entryIdx.internalIndex >= keyCapacity)
                {
                    return false;
                }
            }

            it.NextEntryIndex = (*next)[entryIdx.internalIndex];
            it.EntryIndex = entryIdx;
            
            // Read the value
            //item = Values + entryIdx * sizeof(TValue);
            //item = Values.GetUnsafePtr(entryIdx.listIndex) + entryIdx.elementIndex * sizeof(TValue);
            item = entryIdx.ptrToValue;

            return true;
        }
        
        public static void Destroy(UnsafeParallelListHashMap<TKey, TValue>* hashMap)
        {
            //CheckNull(listData);
            var allocator = hashMap->m_Allocator;
            hashMap->Dispose();
            AllocatorManager.Free(allocator, hashMap);
        }

        public void Dispose()
        {
            UnsafeList<MultipleArrayIndexer>.Destroy(next, ref m_Allocator);
            UnsafeList<MultipleArrayIndexer>.Destroy(buckets, ref m_Allocator);
        }
    }
    
    public unsafe struct UnsafeParallelListHashMapEnumerator<TKey, TValue> 
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        public UnsafeParallelListHashMap<TKey, TValue>* Map;

        public TKey key;
        public bool isFirst;
        private byte* value;

        private UnsafeParallelListHashMapIterator<TKey, TValue> iterator;
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
        
    public struct UnsafeParallelListHashMapIterator<TKey, TValue>
        where TKey : unmanaged
        where TValue : unmanaged
    {
        internal TKey key;
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
        public int internalIndex;
        
        public byte* ptrToKey;
        public byte* ptrToValue;
        
        //public int listIndex;
        //public int elementIndex;

        public static MultipleArrayIndexer Null => new MultipleArrayIndexer()
        {
            internalIndex = -1,
            ptrToKey = null,
            ptrToValue = null,
            //listIndex = 0,
            //elementIndex = 0,
        };
    }
}