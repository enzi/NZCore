// <copyright project="NZCore" file="NodeReader.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    public unsafe ref struct NodeReader
    {
        private readonly byte* _ptr;
        private readonly int _bufferLength;
        private int _offset;

        public byte* CurrentPtr => _ptr + _offset;
        public int Offset => _offset;
        public bool CanRead => _offset < _bufferLength;

        public NodeReader(byte* ptr, int bufferLength)
        {
            _ptr = ptr;
            _bufferLength = bufferLength;
            _offset = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ReadFromNode<T>()
            where T : unmanaged
        {
            ref var node = ref UnsafeUtility.AsRef<T>(_ptr + _offset);
            _offset += UnsafeUtility.SizeOf<T>();
            return ref node;
        }

        public T* ReadRange<T>(int length)
            where T : unmanaged
        {
            var tmp = _ptr + _offset;
            _offset += length * UnsafeUtility.SizeOf<T>();
            return (T*)tmp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOffset(int newOffset)
        {
            _offset += newOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOffset<T>()
            where T : unmanaged
        {
            _offset += UnsafeUtility.SizeOf<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetOffset(int newOffset)
        {
            _offset = newOffset;
        }
    }

    public static class NodeReaderExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe NodeReader AsNodeReader(this ref BlobArray<byte> blobArray) => new((byte*)blobArray.GetUnsafePtr(), blobArray.Length);
    }
}