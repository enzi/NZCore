// <copyright project="Unity.Collections" file="UnsafeParallelHashmapData.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityUnsafe = Unity.Collections.LowLevel.Unsafe;

namespace NZCore.Internal
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct UnsafeParallelHashMapData
    {
        [FieldOffset(0)] public unsafe byte* values;
        [FieldOffset(8)] public unsafe byte* keys;
        [FieldOffset(16 /*0x10*/)] public unsafe byte* next;
        [FieldOffset(24)] public unsafe byte* buckets;
        [FieldOffset(32 /*0x20*/)] public int keyCapacity;
        [FieldOffset(36)] public int bucketCapacityMask;
        [FieldOffset(40)] public int allocatedIndexLength;
        public const int kMaxCapacity = 1073741823 /*0x3FFFFFFF*/;
        public const int IntsPerCacheLine = 16 /*0x10*/;

        private ref Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMapData Self =>
            ref UnsafeUtility.AsRef<Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMapData>(UnsafeUtility.AddressOf(ref this));

        public unsafe int* FirstFreeTLS => Self.firstFreeTLS;
        public UnsafeParallelHashMapBucketData GetBucketData() => Self.GetBucketData();

        public static long GetBucketSize(int capacity) => Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMapData.GetBucketSize(capacity);

        public static int GrowCapacity(int capacity) => Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMapData.GrowCapacity(capacity);

        // Unity allocates thread-local storage after this header; do not allocate it with sizeof alone.
        public static void AllocateHashMap<TKey, TValue>(int length, long bucketLength, AllocatorManager.AllocatorHandle allocator,
            out UnsafeParallelHashMapData* data) where TKey : unmanaged where TValue : unmanaged
        {
            UnityUnsafe.UnsafeParallelHashMapData.AllocateHashMap<TKey, TValue>(length, bucketLength, allocator, out var unityData);
            data = (UnsafeParallelHashMapData*)unityData;
        }

        public static void ReallocateHashMap<TKey, TValue>(UnsafeParallelHashMapData* data, int newCapacity, long newBucketCapacity,
            AllocatorManager.AllocatorHandle allocator) where TKey : unmanaged where TValue : unmanaged =>
            UnityUnsafe.UnsafeParallelHashMapData.ReallocateHashMap<TKey, TValue>((UnityUnsafe.UnsafeParallelHashMapData*)data, newCapacity, newBucketCapacity,
                allocator);

        public static void DeallocateHashMap(UnsafeParallelHashMapData* data, AllocatorManager.AllocatorHandle allocator) =>
            UnityUnsafe.UnsafeParallelHashMapData.DeallocateHashMap((UnityUnsafe.UnsafeParallelHashMapData*)data, allocator);

        public static long CalculateDataSize<TKey, TValue>(int length, long bucketLength, out long keyOffset, out long nextOffset, out long bucketOffset)
            where TKey : unmanaged where TValue : unmanaged =>
            UnityUnsafe.UnsafeParallelHashMapData.CalculateDataSize<TKey, TValue>(length, bucketLength, out keyOffset, out nextOffset, out bucketOffset);

        public static bool IsEmpty(UnsafeParallelHashMapData* data) =>
            UnityUnsafe.UnsafeParallelHashMapData.IsEmpty((UnityUnsafe.UnsafeParallelHashMapData*)data);

        public static int GetCount(UnsafeParallelHashMapData* data) =>
            UnityUnsafe.UnsafeParallelHashMapData.GetCount((UnityUnsafe.UnsafeParallelHashMapData*)data);

        public static bool MoveNextSearch(UnsafeParallelHashMapData* data, ref int bucketIndex, ref int nextIndex, out int index) =>
            UnityUnsafe.UnsafeParallelHashMapData.MoveNextSearch((UnityUnsafe.UnsafeParallelHashMapData*)data, ref bucketIndex, ref nextIndex, out index);

        public static bool MoveNext(UnsafeParallelHashMapData* data, ref int bucketIndex, ref int nextIndex, out int index) =>
            UnityUnsafe.UnsafeParallelHashMapData.MoveNext((UnityUnsafe.UnsafeParallelHashMapData*)data, ref bucketIndex, ref nextIndex, out index);

        public static void GetKeyArray<TKey>(UnsafeParallelHashMapData* data, NativeArray<TKey> result) where TKey : unmanaged =>
            UnityUnsafe.UnsafeParallelHashMapData.GetKeyArray((UnityUnsafe.UnsafeParallelHashMapData*)data, result);

        public static void GetValueArray<TValue>(UnsafeParallelHashMapData* data, NativeArray<TValue> result) where TValue : unmanaged =>
            UnityUnsafe.UnsafeParallelHashMapData.GetValueArray((UnityUnsafe.UnsafeParallelHashMapData*)data, result);

        public static void GetKeyValueArrays<TKey, TValue>(UnsafeParallelHashMapData* data, NativeKeyValueArrays<TKey, TValue> result)
            where TKey : unmanaged where TValue : unmanaged =>
            UnityUnsafe.UnsafeParallelHashMapData.GetKeyValueArrays((UnityUnsafe.UnsafeParallelHashMapData*)data, result);
    }
}