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
        [ReadOnly] private UnsafeList<byte> _data;
        private int _currentIndex;

        public int CurrentIndex => _currentIndex;
        public byte* CurrentPtr => (byte*)_data.Ptr + _currentIndex;
        public bool ReachedLastIndex => _currentIndex >= _data.Length; // >= is safeguard for corrupt savegames to prevent endless loop

        public UnsafeByteDeserializer(UnsafeList<byte> byteList, int offset = 0)
        {
            _data = byteList;
            _currentIndex = offset;
        }

        public ref T Read<T>()
            where T : unmanaged
        {
            var ptr = (byte*)_data.Ptr;
            ref var element = ref *(T*)(ptr + _currentIndex);
            _currentIndex += UnsafeUtility.SizeOf<T>();
            return ref element;
        }

        public T* ReadRange<T>(int length)
            where T : unmanaged
        {
            var ptr = (byte*)_data.Ptr;
            var ptrToData = (T*)(ptr + _currentIndex);
            _currentIndex += length * UnsafeUtility.SizeOf<T>();
            return ptrToData;
        }

        public ref T Peek<T>()
            where T : unmanaged
        {
            var ptr = (byte*)_data.Ptr;
            ref var element = ref *(T*)(ptr + _currentIndex);
            return ref element;
        }

        public void AddOffset(int offset)
        {
            _currentIndex += offset;
        }

        public void AddOffset<T>()
            where T : unmanaged
        {
            _currentIndex += UnsafeUtility.SizeOf<T>();
        }

        public void AddOffsetRange<T>(int length)
            where T : unmanaged
        {
            _currentIndex += UnsafeUtility.SizeOf<T>() * length;
        }

        public void SetOffset(int offset)
        {
            _currentIndex = offset;
        }

        public void Reset()
        {
            _currentIndex = 0;
        }

        public void Dispose()
        {
            _data.Dispose();
        }
    }
}