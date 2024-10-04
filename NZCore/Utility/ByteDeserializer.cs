// <copyright project="NZCore" file="ByteDeserializer.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    public unsafe struct ByteDeserializer
    {
        [ReadOnly] private NativeArray<byte> data;
        private int currentIndex;

        public int CurrentIndex => currentIndex;
        public byte* CurrentPtr => (byte*)data.GetUnsafeReadOnlyPtr() + currentIndex;
        public bool ReachedLastIndex => currentIndex >= data.Length; // >= is safeguard for corrupt savegames to prevent endless loop

        public ByteDeserializer(NativeArray<byte> byteArray, int offset = 0)
        {
            data = byteArray;
            currentIndex = offset;
        }

        public ref T Read<T>()
            where T : unmanaged
        {
            var ptr = (byte*)data.GetUnsafeReadOnlyPtr();
            ref var element = ref *(T*)(ptr + currentIndex);
            currentIndex += UnsafeUtility.SizeOf<T>();
            return ref element;
        }

        public T* ReadRange<T>(int length)
            where T : unmanaged
        {
            var ptr = (byte*)data.GetUnsafeReadOnlyPtr();
            var ptrToData = (T*)(ptr + currentIndex);
            currentIndex += length * UnsafeUtility.SizeOf<T>();
            return ptrToData;
        }

        public ref T Peek<T>()
            where T : unmanaged
        {
            var ptr = (byte*)data.GetUnsafeReadOnlyPtr();
            ref var element = ref *(T*)(ptr + currentIndex);
            return ref element;
        }

        public void AddOffset(int offset)
        {
            currentIndex += offset;
        }

        public void AddOffset<T>()
            where T : unmanaged
        {
            currentIndex += UnsafeUtility.SizeOf<T>();
        }
        
        public void AddOffsetRange<T>(int length)
            where T : unmanaged
        {
            currentIndex += UnsafeUtility.SizeOf<T>() * length;
        }

        public void SetOffset(int offset)
        {
            currentIndex = offset;
        }

        public void Reset()
        {
            currentIndex = 0;
        }
    }
}