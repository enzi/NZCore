using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    public static unsafe class DynamicBufferExtensions
    {
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
        
        public static int RemoveAtSwapBackReportIndex<T>(this DynamicBuffer<T> buffer, int index)
            where T : unmanaged
        {
            buffer.Length -= 1;
            // ref var l = ref buffer.Length;
            // l -= 1;
            // int newLength = l;
            if (index == buffer.Length) 
                return -1;
            
            byte* basePtr = (byte*) buffer.GetUnsafePtr();
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
            //var tmp = (DynamicBufferExposed<T>*)UnsafeUtility.AddressOf(ref buffer);
            ref var tmp = ref UnsafeUtility.As<DynamicBuffer<T>, DynamicBufferExposed<T>>(ref buffer);
            return (BufferHeaderExposed*) tmp.m_Buffer;
        }


        public static void AddBytes<TBuffer, TData>(this DynamicBuffer<TBuffer> buffer, TData data)
            where TData : unmanaged
            where TBuffer : unmanaged
        {
            CheckWriteAccess(buffer);
            
            int elemSize = UnsafeUtility.SizeOf<TData>();
            int oldLength = buffer.Length;
            buffer.ResizeUninitialized(oldLength + elemSize);
            var ptrToData = UnsafeUtility.AddressOf(ref data);
            
            var basePtr = (byte*)buffer.GetUnsafePtr();
            UnsafeUtility.MemCpy(basePtr + oldLength, ptrToData, elemSize);
        }
    }
    
    [StructLayout(LayoutKind.Explicit)]
    [NoAlias]
    public unsafe struct BufferHeaderExposed
    {
        [NoAlias] 
        [FieldOffset(0)] public byte* Pointer;
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
        
        private static void ResizeUninitialized<T>(BufferHeaderExposed* bufferHeader, int length)
            where T : struct
        {
            BufferHeader.EnsureCapacity((BufferHeader*)bufferHeader, length, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), BufferHeader.TrashMode.RetainOldData, false, 0);
            bufferHeader->Length = length;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DynamicBufferExposed<T>
    {
        [NativeDisableUnsafePtrRestriction] [NoAlias]
        internal BufferHeader* m_Buffer;

        // Stores original internal capacity of the buffer header, so heap excess can be removed entirely when trimming.
        private int m_InternalCapacity;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety0;
        internal AtomicSafetyHandle m_Safety1;
        internal int m_SafetyReadOnlyCount;
        internal int m_SafetyReadWriteCount;

        [MarshalAs(UnmanagedType.U1)] internal bool m_IsReadOnly;

        [MarshalAs(UnmanagedType.U1)] internal bool m_useMemoryInitPattern;
        internal byte m_memoryInitPattern;
#endif
    }
}