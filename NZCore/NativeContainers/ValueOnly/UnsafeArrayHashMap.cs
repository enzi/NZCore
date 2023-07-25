using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

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
        
        [NativeDisableUnsafePtrRestriction] private byte* values;
        [NativeDisableUnsafePtrRestriction] private int* buckets;
        [NativeDisableUnsafePtrRestriction] private int* next;
        
        [NativeDisableUnsafePtrRestriction] private UnsafeList<int>* bucketsAndNextList;

        private AllocatorManager.AllocatorHandle m_Allocator;

        public int Length => allocatedIndexLength;
        

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static UnsafeArrayHashMap<TKey, TValue>* Create<TAllocator>(int keyOffset, ref TAllocator allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
            where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
            UnsafeArrayHashMap<TKey, TValue>* unsafeArrayHashMap = allocator.Allocate(default(UnsafeArrayHashMap<TKey, TValue>), 1);

            unsafeArrayHashMap->m_Allocator = allocator.Handle;
            
            unsafeArrayHashMap->values = null;
            
            var listPtr = UnsafeList<int>.Create(0, allocator.Handle, options);
            unsafeArrayHashMap->bucketsAndNextList = listPtr;
            
            unsafeArrayHashMap->next = unsafeArrayHashMap->bucketsAndNextList->Ptr;
            unsafeArrayHashMap->buckets = unsafeArrayHashMap->bucketsAndNextList->Ptr;

            unsafeArrayHashMap->keyCapacity = 0;
            unsafeArrayHashMap->bucketCapacityMask = 0;
            unsafeArrayHashMap->allocatedIndexLength = 0;
            unsafeArrayHashMap->keyOffset = keyOffset;
            
            return unsafeArrayHashMap;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetArrays(ref NativeArray<TValue> valueArray)
        {
            if (valueArray.Length == 0)
            {
                Clear();
                return;
            }
            
            values = (byte*) valueArray.GetUnsafeReadOnlyPtr();

            int length = valueArray.Length;
            int bucketLength = length * 2;

            keyCapacity = length;
            bucketLength = math.ceilpow2(bucketLength);
            bucketCapacityMask = bucketLength - 1;

            //Debug.Log($"Set next/buckets cap to {length}/{bucketLength} - keyOffset: {keyOffset}");
            
            bucketsAndNextList->Resize(length + bucketLength, NativeArrayOptions.UninitializedMemory);
            next = bucketsAndNextList->Ptr;
            buckets = bucketsAndNextList->Ptr + length;
            
            UnsafeUtility.MemSet(next, 0xFF, length * 4); // sets everything to max, Unity uses the same method in their NativeHashMap clear
            UnsafeUtility.MemSet(buckets, 0xFF, bucketLength * 4);
            
            allocatedIndexLength = length;
        }

        private void Clear()
        {
            // set all to -1
            UnsafeUtility.MemSet(buckets, 0xFF, (bucketCapacityMask + 1) * 4);
            UnsafeUtility.MemSet(next, 0xFF, (keyCapacity) * 4);
            
            allocatedIndexLength = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculateBuckets()
        {
            //Debug.Log($"CalculateBuckets with length {allocatedIndexLength} nextCap: {next->Capacity} bucketsCap: {buckets->Capacity}");

            byte* keyArrayPtr = (values + keyOffset);
            var size = sizeof(TValue);
            
            for (int i = 0; i < allocatedIndexLength; i++)
            {
                var bucketIndex = (*(TKey*) keyArrayPtr).GetHashCode() & bucketCapacityMask;
                
                next[i] = buckets[bucketIndex];
                buckets[bucketIndex] = i;

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
            return *(TKey*) (values + index * sizeof(TValue) + keyOffset);
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

            var size = sizeof(TValue);
            byte* keyArrayPtr = values + keyOffset + entryIdx * size;

            while(!(*(TKey*) keyArrayPtr).Equals(it.Key))
            {
                entryIdx = next[entryIdx];
                if (entryIdx < 0 || entryIdx >= keyCapacity)
                {
                    it.NextEntryIndex = -1;
                    it.EntryIndex = -1;
                    item = null;
                    return false;
                }

                keyArrayPtr += size;
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
            
            var size = sizeof(TValue);
            byte* keyArrayPtr = values + keyOffset + entryIdx * size;

            while(!(*(TKey*) keyArrayPtr).Equals(key))
            {
                entryIdx = next[entryIdx];
                if (entryIdx < 0 || entryIdx >= keyCapacity)
                {
                    return false;
                }

                keyArrayPtr += size;
            }

            return true;
        }
        
        public static void Destroy(UnsafeArrayHashMap<TKey, TValue>* hashMap)
        {
            var allocator = hashMap->m_Allocator;
            hashMap->Dispose();
            AllocatorManager.Free(allocator, hashMap);
        }

        public void Dispose()
        {
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