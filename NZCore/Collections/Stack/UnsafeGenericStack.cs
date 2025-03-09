// <copyright project="NZCore" file="UnsafeGenericStack.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace NZCore
{
    public struct OffsetLookup
    {
        public int Offset;
    }

    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct UnsafeGenericStack : INativeDisposable
    {
        internal UnsafeList<byte> List;
        internal UnsafeList<OffsetLookup> OffsetLookup;

        /// <summary>
        /// Determine if the stack is empty.
        /// </summary>
        public bool IsEmpty => List.IsEmpty;

        /// <summary>
        /// Retrieve the number of items on the stack.
        /// </summary>
        public int Length => List.Length;

        /// <summary>
        /// Determine if the stack has been allocated.
        /// </summary>
        public bool IsCreated => List.IsCreated;

        /// <summary>
        /// Create a new stack.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the stack.</param>
        /// <param name="allocator">The allocator for the stack container.</param>
        /// <param name="options">Initialization options for the stack allocation.</param>
        public UnsafeGenericStack(int initialCapacity, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
        {
            List = new UnsafeList<byte>(initialCapacity, allocator, options);
            OffsetLookup = new UnsafeList<OffsetLookup>(initialCapacity, allocator, options);
        }

        /// <summary>
        /// Push an item onto the stack.
        /// </summary>
        /// <param name="data">The data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push<T>(T data)
            where T : unmanaged
        {
            //list.Add(in item);

            int byteSize = UnsafeUtility.SizeOf<T>();
            int oldLength = List.Length;
            List.Resize(oldLength + byteSize, NativeArrayOptions.UninitializedMemory);
            var ptrToData = UnsafeUtility.AddressOf(ref data);

            UnsafeUtility.MemCpy(List.Ptr + oldLength, ptrToData, byteSize);

            OffsetLookup.Add(new OffsetLookup()
            {
                Offset = oldLength
            });
        }

        /// <summary>
        /// Retrieve the item on the top of the stack.
        /// </summary>
        /// <returns>The item returned by reference.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Top<T>()
            where T : unmanaged
        {
            //return ref list.ElementAt(list.m_length - 1);

            return ref UnsafeUtility.AsRef<T>(List.Ptr + List.m_length - UnsafeUtility.SizeOf<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetFromIndex<T>(int index)
            where T : unmanaged
        {
            return ref UnsafeUtility.AsRef<T>(List.Ptr + OffsetLookup[index].Offset);
        }

        /// <summary>
        /// Pop the item on top of the stack.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Pop<T>()
            where T : unmanaged
        {
            //list.Resize(list.m_length - 1);

            var size = UnsafeUtility.SizeOf<T>();

            T data = *(T*)(List.Ptr + (List.m_length) - size);

            List.Resize(List.m_length - size);
            OffsetLookup.Resize(OffsetLookup.m_length - 1);

            return data;
        }

        /// <summary>
        /// Clear the stack from all its items.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            List.Clear();
            OffsetLookup.Clear();
        }

        /// <summary>
        /// Dispose the memory of the stack.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            List.Dispose();
            OffsetLookup.Dispose();
        }

        /// <summary>
        /// Dispose the memory of the stack.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JobHandle Dispose(JobHandle inputDeps)
        {
            var handle1 = List.Dispose(inputDeps);
            var handle2 = OffsetLookup.Dispose(inputDeps);

            return JobHandle.CombineDependencies(handle1, handle2);
        }
    }
}