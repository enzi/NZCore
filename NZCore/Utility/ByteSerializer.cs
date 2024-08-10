// <copyright project="NZCore" file="ByteSerializer.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
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

        public void AddRange<T>(T* value, int elementCount)
            where T : unmanaged
        {
            data.AddRange(value, elementCount * UnsafeUtility.SizeOf<T>());
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
    }
}