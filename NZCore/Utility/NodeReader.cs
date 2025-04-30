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
        private readonly byte* ptr;
        private readonly int bufferLength;
        private int offset;

        public byte* CurrentPtr => ptr + offset;
        public int Offset => offset;
        public bool CanRead => offset < bufferLength;
        
        public NodeReader(byte* ptr, int bufferLength)
        {
            this.ptr = ptr;
            this.bufferLength = bufferLength;
            offset = 0;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ReadFromNode<T>()
            where T : unmanaged
        {
            ref var node = ref UnsafeUtility.AsRef<T>(ptr + offset);
            offset += UnsafeUtility.SizeOf<T>();
            return ref node;
        }

        public T* ReadRange<T>(int length)
            where T : unmanaged
        {
            var tmp = ptr + offset;
            offset += length * UnsafeUtility.SizeOf<T>();
            return (T*) tmp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOffset(int newOffset)
        {
            offset += newOffset;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOffset<T>()
            where T : unmanaged
        {
            offset += UnsafeUtility.SizeOf<T>();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetOffset(int newOffset)
        {
            offset = newOffset;
        }
    }

    public static class NodeReaderExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe NodeReader AsNodeReader(this ref BlobArray<byte> blobArray)
        {
            return new NodeReader((byte*) blobArray.GetUnsafePtr(), blobArray.Length);
        }
    }
}