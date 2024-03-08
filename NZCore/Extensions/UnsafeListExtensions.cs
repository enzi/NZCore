using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NZCore
{
    public static unsafe class UnsafeListExtensions
    {
        public static void  ResizeExact<T>(this ref UnsafeList<T> list, int newCapacity)
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
        
        public static void SetLengthNoResizeMemClear(this ref UnsafeList<byte> list, int size)
        {
            list.m_length = size;
            list.MemClear();
        }
        
        public static void MemCpy(this ref UnsafeList<byte> list, byte* ptr, int size)
        {
            UnsafeUtility.MemCpy(list.Ptr, ptr, size);
        }
        
        public static void MemClear(this ref UnsafeList<byte> list)
        {
            UnsafeUtility.MemClear(list.Ptr, list.m_capacity);
        }
        
        public static void ReinterpretLengthAndCapacity(this ref UnsafeList<byte> list, int size)
        {
            list.m_length /= size;
            list.m_capacity /= size;
        }
    }
}