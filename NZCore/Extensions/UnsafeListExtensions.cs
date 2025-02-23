// <copyright project="NZCore" file="UnsafeListExtensions.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NZCore
{
    public static unsafe class UnsafeListExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ResizeExact<T>(this ref UnsafeList<T> list, int newCapacity)
            where T : unmanaged
        {
            newCapacity = math.max(0, newCapacity);

            CollectionHelper.CheckAllocator(list.Allocator);
            T* newPointer = null;

            var alignOf = UnsafeUtility.AlignOf<T>();
            var sizeOf = sizeof(T);

            if (newCapacity > 0)
            {
                newPointer = (T*)list.Allocator.Allocate(sizeOf, alignOf, newCapacity);

                if (list.Ptr != null && list.m_capacity > 0)
                {
                    var itemsToCopy = math.min(newCapacity, list.Capacity);
                    var bytesToCopy = itemsToCopy * sizeOf;
                    UnsafeUtility.MemCpy(newPointer, list.Ptr, bytesToCopy);
                }
            }

            list.Allocator.Free(list.Ptr, list.Capacity);

            list.Ptr = newPointer;
            list.m_capacity = newCapacity;
            list.m_length = math.min(list.m_length, newCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetLengthNoResizeMemClear(this ref UnsafeList<byte> list, int size)
        {
            list.m_length = size;
            list.MemClear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemCpy(this ref UnsafeList<byte> list, byte* ptr, int size)
        {
            UnsafeUtility.MemCpy(list.Ptr, ptr, size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemClear(this ref UnsafeList<byte> list)
        {
            UnsafeUtility.MemClear(list.Ptr, list.m_capacity);
        }
        
        /// <summary>
        /// Recalculate length and capacity from a <see cref="byte"/> - <see cref="UnsafeList{T}"/>
        /// which brings both length and capacity down the the appropriate size of the actual unmanaged struct T
        /// the list holds
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReinterpretLengthAndCapacity(this ref UnsafeList<byte> list, int size)
        {
            list.m_length /= size;
            list.m_capacity /= size;
        }

        /// <summary>
        /// Recalculate length and capacity from a <see cref="byte"/> - <see cref="UnsafeList{T}"/>
        /// which brings both length and capacity down the the appropriate size of the actual unmanaged struct T
        /// the list holds
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReinterpretLengthAndCapacity<T>(this ref UnsafeList<byte> list)
            where T : unmanaged
        {
            var size = UnsafeUtility.SizeOf<T>();
            ReinterpretLengthAndCapacity(ref list, size);
        }

        public static void AddRange<T>(this ref UnsafeList<T> list, NativeArray<T> array)
            where T : unmanaged
        {
            list.AddRange(array.GetUnsafeReadOnlyPtr(), array.Length);
        }

        public static UnsafeList<T> Clone<T>(this ref UnsafeList<T> list, Allocator allocator)
            where T : unmanaged
        {
            UnsafeList<T> result = new UnsafeList<T>(list.Length, allocator, NativeArrayOptions.UninitializedMemory);
            UnsafeUtility.MemCpy((byte*) result.Ptr, (byte*) list.Ptr, list.Length * UnsafeUtility.SizeOf<T>());
            return result;
        }
        
        public static void Remove<T>(this ref UnsafeList<T> list, T element)
            where T : unmanaged
        {
            for (int i = list.Length - 1; i >= 0; i--)
            {
                if (list[i].GetHashCode() != element.GetHashCode())
                    continue;

                list.RemoveAt(i);
            }
        }
    }
}