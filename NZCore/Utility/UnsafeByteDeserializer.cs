// <copyright project="NZCore" file="ByteDeserializer.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    public unsafe struct UnsafeByteDeserializer : IDisposable
    {
        [ReadOnly] private UnsafeList<byte> data;
        private int currentIndex;

        public int CurrentIndex => currentIndex;
        public byte* CurrentPtr => (byte*) data.Ptr + currentIndex;
        public bool ReachedLastIndex => currentIndex >= data.Length; // >= is safeguard for corrupt savegames to prevent endless loop
        
        public UnsafeByteDeserializer(UnsafeList<byte> byteList, int offset = 0)
        {
            data = byteList;
            currentIndex = offset;
        }

        public ref T Read<T>()
            where T : unmanaged
        {
            var ptr = (byte*)data.Ptr;
            ref var element = ref *(T*)(ptr + currentIndex);
            currentIndex += UnsafeUtility.SizeOf<T>();
            return ref element;
        }

        public T* ReadRange<T>(int length)
            where T : unmanaged
        {
            var ptr = (byte*)data.Ptr;
            var ptrToData = (T*)(ptr + currentIndex);
            currentIndex += length * UnsafeUtility.SizeOf<T>();
            return ptrToData;
        }

        public ref T Peek<T>()
            where T : unmanaged
        {
            var ptr = (byte*)data.Ptr;
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

        public void Dispose()
        {
            data.Dispose();
        }
    }
}