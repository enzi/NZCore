// <copyright project="NZCore" file="UnsafeHashMapExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;

namespace NZCore
{
    public static class UnsafeHashMapExtensions
    {
        public static void Serialize<TKey, TValue>(this UnsafeHashMap<TKey, TValue> unsafeHashMap, ref ByteSerializer serializer)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            Serialize(unsafeHashMap.m_Data, ref serializer);
        }
        
        public static void Serialize(this UnsafeHashMap<byte, byte> unsafeHashMap, ref ByteSerializer serializer, int sizeOfTKey)
        {
            Serialize(unsafeHashMap.m_Data, ref serializer, sizeOfTKey);
        }

        private static void Serialize<TKey>(HashMapHelper<TKey> hashMap, ref ByteSerializer serializer)
            where TKey : unmanaged, IEquatable<TKey>
        {
            Serialize(UnsafeUtility.As<HashMapHelper<TKey>, HashMapHelper<byte>>(ref hashMap), ref serializer, UnsafeUtility.SizeOf<TKey>());
        }

        private static unsafe void Serialize(HashMapHelper<byte> hashMap, ref ByteSerializer serializer, int sizeOfTKey)
        {
            serializer.AddNoResize(hashMap.Count);

            if (hashMap.Count == 0)
            {
                return;
            }
            
            int totalSize = CalculateDataSize(sizeOfTKey, hashMap.Capacity, hashMap.BucketCapacity, hashMap.SizeOfTValue, out int _, out int _, out int _);
            
            serializer.AddNoResize(totalSize);
            serializer.AddNoResize(hashMap.Capacity);
            serializer.AddNoResize(hashMap.Log2MinGrowth);
            serializer.AddNoResize(hashMap.AllocatedIndex);
            serializer.AddNoResize(hashMap.FirstFreeIdx);
            serializer.AddNoResize(sizeOfTKey);
            serializer.AddNoResize(hashMap.SizeOfTValue);
            
            serializer.AddRangeNoResize(hashMap.Ptr, totalSize);
        }
        
        public static void Deserialize<TKey, TValue>(this ref UnsafeHashMap<TKey, TValue> unsafeHashMap, ref ByteDeserializer deserializer, Allocator allocator)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            Deserialize(ref unsafeHashMap.m_Data, ref deserializer, allocator);
        }
        
        public static void Deserialize(this ref UnsafeHashMap<byte, byte> unsafeHashMap, ref ByteDeserializer deserializer, Allocator allocator)
        {
            Deserialize(ref unsafeHashMap.m_Data, ref deserializer, allocator);
        }
        
        public static unsafe void Deserialize(this ref UnsafeHashMap<byte, byte> unsafeHashMap, ref byte* payloadPtr, Allocator allocator)
        {
            Deserialize(ref unsafeHashMap.m_Data, ref payloadPtr, allocator);
        }

        private static void Deserialize<TKey>(ref HashMapHelper<TKey> hashMap, ref ByteDeserializer deserializer, Allocator allocator)
            where TKey : unmanaged, IEquatable<TKey>
        {
            Deserialize(ref UnsafeUtility.As<HashMapHelper<TKey>, HashMapHelper<byte>>(ref hashMap), ref deserializer, allocator);
        }

        private static unsafe void Deserialize(ref HashMapHelper<byte> hashMap, ref ByteDeserializer deserializer, Allocator allocator)
        {
            var count = deserializer.Read<int>();

            if (count == 0)
            {
                hashMap.Clear();
                return;
            }
            
            hashMap.Dispose();
            
            var totalSize = deserializer.Read<int>();

            hashMap.Count = count;
            hashMap.Capacity = deserializer.Read<int>();
            hashMap.Log2MinGrowth = deserializer.Read<int>();
            hashMap.BucketCapacity = hashMap.Capacity * 2;
            hashMap.AllocatedIndex = deserializer.Read<int>();
            hashMap.FirstFreeIdx = deserializer.Read<int>();
            var sizeOfTKey = deserializer.Read<int>();
            hashMap.SizeOfTValue = deserializer.Read<int>();
            
            CalculateDataSize(sizeOfTKey, hashMap.Capacity, hashMap.BucketCapacity, hashMap.SizeOfTValue, out var keyOffset, out var nextOffset, out var bucketOffset);
            
            hashMap.Ptr = (byte*) Memory.Unmanaged.Allocate(totalSize, JobsUtility.CacheLineSize, allocator);
            hashMap.Keys = (byte*) (hashMap.Ptr + keyOffset);
            hashMap.Next = (int*) (hashMap.Ptr + nextOffset);
            hashMap.Buckets = (int*) (hashMap.Ptr + bucketOffset);
            
            var data = deserializer.ReadRange<byte>(totalSize);
            
            // write back keys, next, buckets and values
            UnsafeUtility.MemCpy(hashMap.Ptr, data, totalSize);
        }

        private static unsafe void Deserialize(ref HashMapHelper<byte> hashMap, ref byte* ptr, Allocator allocator)
        {
            var count = *(int*) (ptr + 0);

            if (count == 0)
            {
                ptr += 4;
                hashMap.Clear();
                return;
            }
            
            hashMap.Dispose();

            hashMap.Count = count;
            var totalSize = *(int*) (ptr + 4);
            hashMap.Capacity = *(int*) (ptr + 8);
            hashMap.Log2MinGrowth = *(int*) (ptr + 12);
            hashMap.AllocatedIndex = *(int*) (ptr + 16);
            hashMap.FirstFreeIdx = *(int*) (ptr + 20);
            var sizeOfTKey = *(int*) (ptr + 24);
            hashMap.SizeOfTValue = *(int*) (ptr + 28);
            
            ptr += 8 * 4;
            
            hashMap.BucketCapacity = hashMap.Capacity * 2;
            
            CalculateDataSize(sizeOfTKey, hashMap.Capacity, hashMap.BucketCapacity, hashMap.SizeOfTValue, out var keyOffset, out var nextOffset, out var bucketOffset);
            
             hashMap.Ptr = (byte*) Memory.Unmanaged.Allocate(totalSize, JobsUtility.CacheLineSize, allocator);
             hashMap.Keys = (byte*) (hashMap.Ptr + keyOffset);
             hashMap.Next = (int*) (hashMap.Ptr + nextOffset);
             hashMap.Buckets = (int*) (hashMap.Ptr + bucketOffset);
            
             // write back keys, next, buckets and values
             UnsafeUtility.MemCpy(hashMap.Ptr, ptr, totalSize);
            ptr += totalSize;
        }
        
        public static int CalculateDataSize(UnsafeHashMap<byte, byte> unsafeHashMap, int sizeOfTKey)
        {
            if (unsafeHashMap.Count == 0)
            {
                return 4;
            }
            
            const int sizeOfInt = sizeof(int);

            var valuesSize = unsafeHashMap.m_Data.SizeOfTValue * unsafeHashMap.Capacity;
            var keysSize = sizeOfTKey * unsafeHashMap.Capacity;
            var nextSize = sizeOfInt * unsafeHashMap.Capacity;
            var bucketSize = sizeOfInt * unsafeHashMap.m_Data.BucketCapacity;
            var totalSize = valuesSize + keysSize + nextSize + bucketSize;

            return totalSize;
        }

        public static int CalculateDataSize(int sizeOfTKey, int capacity, int bucketCapacity, int sizeOfTValue, out int outKeyOffset, out int outNextOffset, out int outBucketOffset)
        {
            const int sizeOfInt = sizeof(int);

            var valuesSize = sizeOfTValue * capacity;
            var keysSize = sizeOfTKey * capacity;
            var nextSize = sizeOfInt * capacity;
            var bucketSize = sizeOfInt * bucketCapacity;
            var totalSize = valuesSize + keysSize + nextSize + bucketSize;

            outKeyOffset = 0 + valuesSize;
            outNextOffset = outKeyOffset + keysSize;
            outBucketOffset = outNextOffset + nextSize;

            return totalSize;
        }

        public static unsafe NativeArray<byte> GetUntypedKeyArray(UnsafeHashMap<byte, byte> unsafeHashMap, int keySize)
        {
            var result = CollectionHelper.CreateNativeArray<byte>(unsafeHashMap.Count * keySize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            if (!unsafeHashMap.IsCreated)
            {
                return result;
            }
            
            var resultPtr = (byte*) result.GetUnsafePtr();
            
            for (int i = 0, count = 0, max = result.Length, capacity = unsafeHashMap.m_Data.BucketCapacity
                 ; i < capacity && count < max
                 ; ++i
                )
            {
                int bucket = unsafeHashMap.m_Data.Buckets[i];

                while (bucket != -1)
                {
                    UnsafeUtility.MemCpy(resultPtr, unsafeHashMap.m_Data.Keys + bucket * keySize, keySize);
                    
                    bucket = unsafeHashMap.m_Data.Next[bucket];
                    resultPtr += keySize;
                }
            }

            return result;
        }

        public static unsafe NativeArray<byte> GetUntypedValueArray(UnsafeHashMap<byte, byte> unsafeHashMap, int valueSize)
        {
            var result = CollectionHelper.CreateNativeArray<byte>(unsafeHashMap.Count * valueSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var resultPtr = (byte*)result.GetUnsafePtr();

            for (int i = 0, count = 0, max = result.Length, capacity = unsafeHashMap.m_Data.BucketCapacity;
                 i < capacity && count < max;
                 ++i
                )
            {
                int bucket = unsafeHashMap.m_Data.Buckets[i];

                while (bucket != -1)
                {
                    UnsafeUtility.MemCpy(resultPtr, unsafeHashMap.m_Data.Ptr + bucket * valueSize, valueSize);

                    bucket = unsafeHashMap.m_Data.Next[bucket];
                    resultPtr += valueSize;
                }
            }

            return result;
        }
    }
}