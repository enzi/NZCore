using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore.Internal
{
    // Direct calls only: Unity owns the implementations, including their safety and allocator behavior.
    public static unsafe class CollectionInternals
    {
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        internal static void CheckAllocator(AllocatorManager.AllocatorHandle allocator) => CollectionHelper.CheckAllocator(allocator);

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        internal static void CheckIsUnmanaged<T>() => CollectionHelper.CheckIsUnmanaged<T>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal static void InitNativeContainer<T>(AtomicSafetyHandle safety) => CollectionHelper.InitNativeContainer<T>(safety);

        internal static int AddSafetyHandle(AllocatorManager.AllocatorHandle allocator, AtomicSafetyHandle safety) => allocator.AddSafetyHandle(safety);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AtomicSafetyHandle GetSafety<T>(this NativeList<T> list) where T : unmanaged => list.m_Safety;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AtomicSafetyHandle GetSafety<TKey, TValue>(this NativeHashMap<TKey, TValue> hashMap)
            where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged => hashMap.m_Safety;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AtomicSafetyHandle GetSafety<TKey, TValue>(this NativeParallelHashMap<TKey, TValue> hashMap)
            where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged => hashMap.m_Safety;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AtomicSafetyHandle GetSafety<TKey, TValue>(this NativeParallelMultiHashMap<TKey, TValue> hashMap)
            where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged => hashMap.m_Safety;
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnsafeList<T>* GetData<T>(this NativeList<T> list) where T : unmanaged => list.m_ListData;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref HashMapHelper<TKey> GetData<TKey, TValue>(this ref UnsafeHashMap<TKey, TValue> hashMap)
            where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged =>
            ref UnsafeUtility.As<Unity.Collections.LowLevel.Unsafe.HashMapHelper<TKey>, HashMapHelper<TKey>>(ref hashMap.m_Data);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HashMapHelper<TKey>* GetData<TKey, TValue>(this NativeHashMap<TKey, TValue> hashMap)
            where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged => (HashMapHelper<TKey>*)hashMap.m_Data;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnsafeParallelHashMapData* GetData<TKey, TValue>(this NativeParallelHashMap<TKey, TValue> hashMap)
            where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged => (UnsafeParallelHashMapData*)hashMap.m_HashMapData.m_Buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnsafeParallelHashMapData* GetData<TKey, TValue>(this NativeParallelMultiHashMap<TKey, TValue> hashMap)
            where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged => (UnsafeParallelHashMapData*)hashMap.m_MultiHashMapData.m_Buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnsafeParallelHashMapData* GetData<TKey, TValue>(this NativeParallelMultiHashMap<TKey, TValue>.ParallelWriter hashMap)
            where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged => (UnsafeParallelHashMapData*)hashMap.m_Writer.m_Buffer;

        internal static T* Allocate<TAllocator, T>(ref TAllocator allocator, T value, int count)
            where TAllocator : unmanaged, AllocatorManager.IAllocator where T : unmanaged => AllocatorManager.Allocate(ref allocator, value, count);

        internal static void DestroyList<T, TAllocator>(UnsafeList<T>* list, ref TAllocator allocator)
            where T : unmanaged where TAllocator : unmanaged, AllocatorManager.IAllocator => UnsafeList<T>.Destroy(list, ref allocator);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T* GetBuffer<T>(this ref FixedList4096Bytes<T> list) where T : unmanaged => (T*)list.Buffer;
    }
}