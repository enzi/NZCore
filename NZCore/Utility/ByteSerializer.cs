// <copyright project="NZCore" file="ByteSerializer.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    public unsafe struct ByteSerializer
    {
        private NativeList<byte> data;

        public NativeList<byte> Data => data;
        public int Length => data.Length;

        public ByteSerializer(NativeList<byte> existingList)
        {
            data = existingList;
        }

        public ByteSerializer(int capacity, Allocator allocator)
        {
            data = new NativeList<byte>(capacity, allocator);
        }

        public void AddCapacity(int capacity)
        {
            if (data.Length + capacity > data.Capacity)
            {
                data.Capacity = data.Length + capacity;
            }
        }

        public int Allocate(int byteAmount)
        {
            var tmpIndex = data.Length;
            data.ResizeUninitialized(data.Length + byteAmount);
            return tmpIndex;
        }

        public int Allocate<T>()
            where T : unmanaged
        {
            return Allocate(UnsafeUtility.SizeOf<T>());
        }

        public int Allocate<T>(int elementCount)
            where T : unmanaged
        {
            return Allocate(elementCount * UnsafeUtility.SizeOf<T>());
        }

        public void Add<T>(T value)
            where T : unmanaged
        {
            Add((byte*)&value, UnsafeUtility.SizeOf<T>());
        }

        public void Add(byte* ptrToData, int byteSize)
        {
            int oldLength = data.Length;
            data.ResizeUninitialized(oldLength + byteSize);

            byte* ptr = (byte*)data.GetUnsafePtr();
            UnsafeUtility.MemCpy(ptr + oldLength, ptrToData, byteSize);
        }

        public void AddRange<T>(NativeArray<T> value)
            where T : unmanaged
        {
            data.AddRange(value.GetUnsafeReadOnlyPtr(), value.Length * UnsafeUtility.SizeOf<T>());
        }

        public void AddRange<T>(T* valuePtr, int elementCount)
            where T : unmanaged
        {
            data.AddRange(valuePtr, elementCount * UnsafeUtility.SizeOf<T>());
        }

        // no resize methods
        public void AddNoResize<T>(T value)
            where T : unmanaged
        {
            data.AddRangeNoResize(&value, UnsafeUtility.SizeOf<T>());
        }
        
        public void AddNoResize(byte* ptrToData, int byteSize)
        {
            AddRangeNoResize(ptrToData, byteSize);
        }
        
        public void AddRangeNoResize<T>(T* valuePtr, int length)
            where T : unmanaged
        {
            data.AddRangeNoResize(valuePtr, length * UnsafeUtility.SizeOf<T>());
        }

        public void AddRangeNoResize<T>(NativeArray<T> value)
            where T : unmanaged
        {
            data.AddRangeNoResize(value.GetUnsafeReadOnlyPtr(), value.Length * UnsafeUtility.SizeOf<T>());
        }
        
        public void AddRangeNoResize<T>(NativeArray<T> value, int length)
            where T : unmanaged
        {
            data.AddRangeNoResize(value.GetUnsafeReadOnlyPtr(), length * UnsafeUtility.SizeOf<T>());
        }

        public ref T GetRef<T>(int index)
            where T : unmanaged
        {
            var ptr = data.GetUnsafePtr();
            return ref *(T*)(ptr + index);
        }

        public void Set<T>(int index, T payload)
            where T : unmanaged
        {
            ref var tmp = ref GetRef<T>(index);
            tmp = payload;
        }
        
        public T* GetPtr<T>(int index)
            where T : unmanaged
        {
            var ptr = data.GetUnsafePtr();
            return (T*) (ptr + index);
        }
    }
}