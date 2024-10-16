// <copyright project="NZCore" file="DynamicBufferExtensions.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    public static unsafe class DynamicBufferExtensions
    {
        public static void MemClear<T>(this DynamicBuffer<T> buffer)
            where T : unmanaged, IBufferElementData
        {
            var ptr = buffer.GetUnsafePtr();
            UnsafeUtility.MemClear(ptr, buffer.Length);
        }

        public static void UnsafeClear<T>(this DynamicBuffer<T> buffer)
            where T : unmanaged, IBufferElementData
        {
            buffer.GetBufferHeader()->Length = 0;
        }

        public static int GetInternalCapacity<T>(this BufferTypeHandle<T> bufferHandle)
            where T : unmanaged, IBufferElementData
        {
            return TypeManager.GetTypeInfo(bufferHandle.m_TypeIndex).BufferCapacity;
        }

        public static int GetInternalCapacity<T>(this BufferLookup<T> bufferLookup)
            where T : unmanaged, IBufferElementData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            return TypeManager.GetTypeInfo(typeIndex).BufferCapacity;
        }

        public static void Remove<T>(this DynamicBuffer<T> buffer, T element)
            where T : unmanaged
        {
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].GetHashCode() != element.GetHashCode())
                    continue;

                buffer.RemoveAt(i);
            }
        }

        public static int RemoveAtSwapBackReportIndex<T>(this DynamicBuffer<T> buffer, int index)
            where T : unmanaged
        {
            buffer.Length -= 1;
            // ref var l = ref buffer.Length;
            // l -= 1;
            // int newLength = l;
            if (index == buffer.Length)
                return -1;

            byte* basePtr = (byte*)buffer.GetUnsafePtr();
            UnsafeUtility.WriteArrayElement(basePtr, index, UnsafeUtility.ReadArrayElement<T>(basePtr, buffer.Length));

            return index;
        }

        public static void AddRange<T>(this DynamicBuffer<T> buffer, T* ptr, int length)
            where T : unmanaged
        {
            CheckWriteAccess(buffer);

            int elemSize = UnsafeUtility.SizeOf<T>();
            int oldLength = buffer.Length;
            buffer.ResizeUninitialized(oldLength + length);

            var basePtr = (byte*)buffer.GetUnsafePtr();
            UnsafeUtility.MemCpy(basePtr + ((long)oldLength * elemSize), ptr, (long)elemSize * length);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckWriteAccess<T>(DynamicBuffer<T> buffer)
            where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(buffer.m_Safety0);
            AtomicSafetyHandle.CheckWriteAndThrow(buffer.m_Safety1);
#endif
        }

        public static BufferHeaderExposed* GetBufferHeader<T>(this ref DynamicBuffer<T> buffer)
            where T : unmanaged
        {
            ref var bufferExposed = ref UnsafeUtility.As<DynamicBuffer<T>, DynamicBufferExposed<T>>(ref buffer);
            return (BufferHeaderExposed*)bufferExposed.m_Buffer;
        }

        public static void AddToByteBuffer<TBuffer, TData>(this DynamicBuffer<TBuffer> buffer, TData data)
            where TData : unmanaged
            where TBuffer : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckWriteAccess(buffer);
            if (UnsafeUtility.SizeOf<TBuffer>() != 1)
                throw new Exception("Usage is only designed for byte buffer!");
#endif

            int byteSize = UnsafeUtility.SizeOf<TData>();
            int oldLength = buffer.Length;
            buffer.ResizeUninitialized(oldLength + byteSize);
            var ptrToData = UnsafeUtility.AddressOf(ref data);

            var basePtr = (byte*)buffer.GetUnsafePtr();
            UnsafeUtility.MemCpy(basePtr + oldLength, ptrToData, byteSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BufferHeaderExposed* GetBufferHeader<T>(this ref BufferAccessor<T> bufferAccessor, int index)
            where T : unmanaged, IBufferElementData
        {
            ref var exposed = ref GetBufferAccessorHeader(ref bufferAccessor);
            return (BufferHeaderExposed*)(exposed.m_BasePointer + index * exposed.m_Stride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref BufferAccessorExposed<T> GetBufferAccessorHeader<T>(this ref BufferAccessor<T> bufferAccessor)
            where T : unmanaged, IBufferElementData
        {
            var ptr = UnsafeUtility.AddressOf(ref bufferAccessor);
            return ref UnsafeUtility.AsRef<BufferAccessorExposed<T>>(ptr);
        }

        public static bool IsEmpty<T>(this ref BufferAccessor<T> bufferAccessor)
            where T : unmanaged, IBufferElementData
        {
            ref var exposed = ref bufferAccessor.GetBufferAccessorHeader();

            byte* basePointer = exposed.m_BasePointer;
            var stride = exposed.m_Stride;
            int length = exposed.m_Length;

            int count = 0;
            for (int i = 0; i < length; i++)
            {
                var tmp = ((BufferHeaderExposed*)(basePointer + i * stride));
                count += tmp->Length;
            }

            return count == 0;
        }
    }
}