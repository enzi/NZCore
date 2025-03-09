// <copyright project="NZCore" file="NativeListExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace NZCore
{
    public static unsafe class NativeListExtensions
    {
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckWriteAccess<T>(NativeList<T> list)
            where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(list.m_Safety);
#endif
        }

        public static void AddArrayToBlob<T>(this NativeList<T> nativeList, ref BlobBuilder builder, ref BlobArray<T> blobArray)
            where T : unmanaged
        {
            nativeList.AsArray().AddArrayToBlob(ref builder, ref blobArray);
        }

        public static void AddToByteList<TData>(this NativeList<byte> list, TData data)
            where TData : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckWriteAccess(list);
#endif

            int byteSize = UnsafeUtility.SizeOf<TData>();
            var ptrToData = (byte*)UnsafeUtility.AddressOf(ref data);

            AddToByteList(list, ptrToData, byteSize);
        }

        public static void AddToByteList(this NativeList<byte> list, byte* ptrToData, int byteSize)
        {
            int oldLength = list.Length;
            list.ResizeUninitialized(oldLength + byteSize);

            var basePtr = (byte*)list.GetUnsafePtr();
            UnsafeUtility.MemCpy(basePtr + oldLength, ptrToData, byteSize);
        }

        public static void AddZeroToByteList(this NativeList<byte> list, int byteSize)
        {
            int oldLength = list.Length;
            list.ResizeUninitialized(oldLength + byteSize);
            
            var basePtr = (byte*)list.GetUnsafePtr();
            UnsafeUtility.MemClear(basePtr + oldLength, byteSize);
        }
        
        public static void AddRangeToByteList<TData>(this NativeList<byte> list, NativeArray<TData> array)
            where TData : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckWriteAccess(list);
#endif

            int byteSize = UnsafeUtility.SizeOf<TData>() * array.Length;
            var ptrToData = (byte*) array.GetUnsafeReadOnlyPtr();

            AddToByteList(list, ptrToData, byteSize);
        }

        public static void MemCpy(this NativeList<byte> list, byte* ptr, int size)
        {
            UnsafeUtility.MemCpy(list.m_ListData->Ptr, ptr, size);
        }

        public static void MemClear(this NativeList<byte> list)
        {
            UnsafeUtility.MemClear(list.m_ListData->Ptr, list.m_ListData->m_capacity);
        }

        public static void ReinterpretLengthAndCapacity<T>(this NativeList<byte> list)
            where T : unmanaged
        {
            var size = UnsafeUtility.SizeOf<T>();

            ReinterpretLengthAndCapacity(list, size);
        }

        public static void ReinterpretLengthAndCapacity(this NativeList<byte> list, int size)
        {
            list.m_ListData->m_length /= size;
            list.m_ListData->m_capacity /= size;
        }

        public static void SetLengthNoResizeMemClear(this NativeList<byte> list, int size)
        {
            list.m_ListData->m_length = size;
            list.MemClear();
        }

        public static void ResizeExact<T>(this NativeList<T> list, int newCapacity)
            where T : unmanaged
        {
            newCapacity = math.max(0, newCapacity);
            var listPtr = list.m_ListData;
            var allocator = listPtr->Allocator;

            CollectionHelper.CheckAllocator(allocator);
            T* newPointer = null;

            var alignOf = UnsafeUtility.AlignOf<T>();
            var sizeOf = sizeof(T);

            if (newCapacity > 0)
            {
                newPointer = (T*)allocator.Allocate(sizeOf, alignOf, newCapacity);

                if (listPtr->Ptr != null && listPtr->m_capacity > 0)
                {
                    var itemsToCopy = math.min(newCapacity, listPtr->Capacity);
                    var bytesToCopy = itemsToCopy * sizeOf;
                    UnsafeUtility.MemCpy(newPointer, listPtr->Ptr, bytesToCopy);
                }
            }

            allocator.Free(listPtr->Ptr, listPtr->Capacity);

            listPtr->Ptr = newPointer;
            listPtr->m_capacity = newCapacity;
            listPtr->m_length = math.min(listPtr->m_length, newCapacity);
        }
        
        public static void ResizeExact<T>(this NativeList<T> list, int newCapacity, int alignOf)
            where T : unmanaged
        {
            newCapacity = math.max(0, newCapacity);
            var listPtr = list.m_ListData;
            var allocator = listPtr->Allocator;

            CollectionHelper.CheckAllocator(allocator);
            T* newPointer = null;
           
            var sizeOf = sizeof(T);

            if (newCapacity > 0)
            {
                newPointer = (T*)allocator.Allocate(sizeOf, alignOf, newCapacity);

                if (listPtr->Ptr != null && listPtr->m_capacity > 0)
                {
                    var itemsToCopy = math.min(newCapacity, listPtr->Capacity);
                    var bytesToCopy = itemsToCopy * sizeOf;
                    UnsafeUtility.MemCpy(newPointer, listPtr->Ptr, bytesToCopy);
                }
            }

            allocator.Free(listPtr->Ptr, listPtr->Capacity);

            listPtr->Ptr = newPointer;
            listPtr->m_capacity = newCapacity;
            listPtr->m_length = math.min(listPtr->m_length, newCapacity);
        }

        public static void Remove<T>(this NativeList<T> list, T element)
            where T : unmanaged
        {
            for (int i = list.Length - 1; i >= 0; i--)
            {
                if (list[i].GetHashCode() != element.GetHashCode())
                    continue;

                list.RemoveAt(i);
            }
        }

        public static ref readonly T ElementAtRO<T>(this NativeList<T> list, int index)
            where T : unmanaged
        {
            return ref UnsafeUtility.ArrayElementAsRef<T>(list.GetUnsafeReadOnlyPtr(), index);
        }
    }
}