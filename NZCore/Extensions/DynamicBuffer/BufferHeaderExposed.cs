// <copyright project="NZCore" file="BufferHeaderExposed.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    [StructLayout(LayoutKind.Explicit)]
    [NoAlias]
    public unsafe struct BufferHeaderExposed
    {
        [NoAlias] [FieldOffset(0)] public byte* Pointer;
        [FieldOffset(8)] public int Length;
        [FieldOffset(12)] public int Capacity;

        public static byte* GetElementPointer(BufferHeaderExposed* header)
        {
            if (header->Pointer != null)
                return header->Pointer;

            return (byte*)(header + 1);
        }

        public static void RemoveAt<T>(BufferHeaderExposed* bufferHeader, int index)
            where T : unmanaged
        {
            RemoveRange<T>(bufferHeader, index, 1);
        }

        public static void RemoveRange<T>(BufferHeaderExposed* bufferHeader, int index, int count)
            where T : unmanaged
        {
            //CheckWriteAccess();
            //CheckBounds(index);
            if (count == 0)
                return;
            //CheckBounds(index + count - 1);

            int elemSize = UnsafeUtility.SizeOf<T>();
            byte* basePtr = GetElementPointer(bufferHeader);

            UnsafeUtility.MemMove(basePtr + index * elemSize, basePtr + (index + count) * elemSize, (long)elemSize * (bufferHeader->Length - count - index));

            bufferHeader->Length -= count;
        }

        public static void RemoveAtSwapBack<T>(BufferHeaderExposed* bufferHeader, int index)
            where T : struct
        {
            ref var l = ref bufferHeader->Length;
            l -= 1;
            int newLength = l;
            if (index == newLength)
                return;

            byte* basePtr = GetElementPointer(bufferHeader);
            UnsafeUtility.WriteArrayElement(basePtr, index, UnsafeUtility.ReadArrayElement<T>(basePtr, newLength));
        }

        public static int RemoveAtSwapBackReportIndex<T>(BufferHeaderExposed* bufferHeader, int index)
            where T : struct
        {
            bufferHeader->Length -= 1;
            // ref var l = ref buffer.Length;
            // l -= 1;
            // int newLength = l;
            if (index == bufferHeader->Length)
                return -1;

            byte* basePtr = GetElementPointer(bufferHeader);
            UnsafeUtility.WriteArrayElement(basePtr, index, UnsafeUtility.ReadArrayElement<T>(basePtr, bufferHeader->Length));

            return index;
        }

        public static void AddRange<T>(BufferHeaderExposed* bufferHeader, T* ptr, int length)
            where T : unmanaged
        {
            int elemSize = UnsafeUtility.SizeOf<T>();
            int oldLength = bufferHeader->Length;

            ResizeUninitialized<T>(bufferHeader, oldLength + length);

            var basePtr = GetElementPointer(bufferHeader);
            UnsafeUtility.MemCpy(basePtr + ((long)oldLength * elemSize), ptr, (long)elemSize * length);
        }

        public static void ClearRange<T>(BufferHeaderExposed* bufferHeader, int length)
            where T : unmanaged
        {
            int elemSize = UnsafeUtility.SizeOf<T>();
            int oldLength = bufferHeader->Length;

            ResizeUninitialized<T>(bufferHeader, oldLength + length);

            var basePtr = GetElementPointer(bufferHeader);
            UnsafeUtility.MemClear(basePtr + ((long)oldLength * elemSize), (long)elemSize * length);
        }

        private static void ResizeUninitialized<T>(BufferHeaderExposed* bufferHeader, int length)
            where T : struct
        {
            BufferHeader.EnsureCapacity((BufferHeader*)bufferHeader, length, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), BufferHeader.TrashMode.RetainOldData, false, 0);
            bufferHeader->Length = length;
        }
    }
}