// <copyright project="NZCore" file="NodeReader.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    public unsafe struct NodeReader
    {
        public byte* Ptr;
        
        public NodeReader(byte* ptr)
        {
            Ptr = ptr;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ReadFromNode<T>()
            where T : unmanaged
        {
            ref var node = ref UnsafeUtility.AsRef<T>(Ptr);
            Ptr += UnsafeUtility.SizeOf<T>();
            return ref node;
        }

        public T* ReadRange<T>(int length)
            where T : unmanaged
        {
            var tmp = Ptr;
            Ptr += length * UnsafeUtility.SizeOf<T>();
            return (T*) tmp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOffset(int offset)
        {
            Ptr += offset;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOffset<T>()
            where T : unmanaged
        {
            Ptr += UnsafeUtility.SizeOf<T>();
        }
    }

    public static class NodeReaderExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe NodeReader AsNodeReader(this ref BlobArray<byte> blobArray)
        {
            return new NodeReader((byte*) blobArray.GetUnsafePtr());
        }
    }
}