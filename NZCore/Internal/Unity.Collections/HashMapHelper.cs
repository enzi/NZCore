using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityUnsafe = Unity.Collections.LowLevel.Unsafe;

namespace NZCore.Internal
{
    // Keep the field order identical to Unity's helper; all operations delegate to Unity.
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct HashMapHelper<TKey> where TKey : unmanaged, IEquatable<TKey>
    {
        [NativeDisableUnsafePtrRestriction] public byte* Ptr;
        [NativeDisableUnsafePtrRestriction] public TKey* Keys;
        [NativeDisableUnsafePtrRestriction] public int* Next;
        [NativeDisableUnsafePtrRestriction] public int* Buckets;
        public int Count;
        public int Capacity;
        public int Log2MinGrowth;
        public int BucketCapacity;
        public int AllocatedIndex;
        public int FirstFreeIdx;
        public int SizeOfTValue;
        public AllocatorManager.AllocatorHandle Allocator;

        public const int kMinCapacity = UnityUnsafe.HashMapHelper<TKey>.kMinCapacity;
        public const int kMaxCapacity = UnityUnsafe.HashMapHelper<TKey>.kMaxCapacity;

        private ref UnityUnsafe.HashMapHelper<TKey> Self => ref UnsafeUtility.AsRef<UnityUnsafe.HashMapHelper<TKey>>(UnsafeUtility.AddressOf(ref this));

        public bool IsCreated => Self.IsCreated;
        public bool IsEmpty => Self.IsEmpty;

        public int CalcCapacityCeilPow2(int capacity) => Self.CalcCapacityCeilPow2(capacity);
        public static int GetBucketSize(int capacity) => UnityUnsafe.HashMapHelper<TKey>.GetBucketSize(capacity);
        public void Clear() => Self.Clear();

        public void Init(int capacity, int sizeOfValueT, int minGrowth, AllocatorManager.AllocatorHandle allocator) =>
            Self.Init(capacity, sizeOfValueT, minGrowth, allocator);

        public void Dispose() => Self.Dispose();

        public static HashMapHelper<TKey>* Alloc(int capacity, int sizeOfValueT, int minGrowth, AllocatorManager.AllocatorHandle allocator) =>
            (HashMapHelper<TKey>*)UnityUnsafe.HashMapHelper<TKey>.Alloc(capacity, sizeOfValueT, minGrowth, allocator);

        public static void Free(HashMapHelper<TKey>* data) => UnityUnsafe.HashMapHelper<TKey>.Free((UnityUnsafe.HashMapHelper<TKey>*)data);
        public void Resize(int newCapacity) => Self.Resize(newCapacity);
        public void ResizeExact(int newCapacity, int newBucketCapacity) => Self.ResizeExact(newCapacity, newBucketCapacity);
        public void TrimExcess() => Self.TrimExcess();

        public static long
            CalculateDataSize(int capacity, int bucketCapacity, int sizeOfTValue, out long keyOffset, out long nextOffset, out long bucketOffset) =>
            UnityUnsafe.HashMapHelper<TKey>.CalculateDataSize(capacity, bucketCapacity, sizeOfTValue, out keyOffset, out nextOffset, out bucketOffset);

        public int GetCount() => Self.GetCount();
        public int TryAdd(in TKey key) => Self.TryAdd(in key);
        public int Find(TKey key) => Self.Find(key);
        public bool TryGetValue<TValue>(TKey key, out TValue item) where TValue : unmanaged => Self.TryGetValue(key, out item);
        public int TryRemove(TKey key) => Self.TryRemove(key);
        public bool MoveNextSearch(ref int bucketIndex, ref int nextIndex, out int index) => Self.MoveNextSearch(ref bucketIndex, ref nextIndex, out index);
        public bool MoveNext(ref int bucketIndex, ref int nextIndex, out int index) => Self.MoveNext(ref bucketIndex, ref nextIndex, out index);
        public NativeArray<TKey> GetKeyArray(AllocatorManager.AllocatorHandle allocator) => Self.GetKeyArray(allocator);

        public NativeArray<TValue> GetValueArray<TValue>(AllocatorManager.AllocatorHandle allocator) where TValue : unmanaged =>
            Self.GetValueArray<TValue>(allocator);

        public NativeKeyValueArrays<TKey, TValue> GetKeyValueArrays<TValue>(AllocatorManager.AllocatorHandle allocator) where TValue : unmanaged =>
            Self.GetKeyValueArrays<TValue>(allocator);
    }
}