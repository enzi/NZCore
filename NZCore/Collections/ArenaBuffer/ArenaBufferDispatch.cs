// <copyright project="NZCore" file="ArenaBufferDispatch.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;

namespace NZCore
{
    /// <summary>
    /// The handful of arena operations that run without knowing the element type, over either allocator.
    ///
    /// The reserve and release systems serve every registered type through one <see cref="ArenaBufferRefData"/>
    /// shaped reinterpret, so they cannot be generic over the allocator the way a buffer can. Keeping the mode
    /// branch here rather than at each call site means there is exactly one place per operation that has to
    /// know two allocators exist.
    ///
    /// Every one of these is a cold path - reserving after a structural change, freeing on destruction,
    /// teardown - so the branch costs nothing that matters. The hot path, resolving a handle, is deliberately
    /// not here: <see cref="ArenaBuffer{T}"/> and <see cref="ContiguousArenaBuffer{T}"/> each resolve directly
    /// against their own allocator, with no branch at all.
    /// </summary>
    public static unsafe class ArenaBufferDispatch
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Reallocate(IntPtr arena, ArenaAllocatorMode mode, ref ArenaBufferRefData refData, int requiredCount, int elementSize)
        {
            if (mode == ArenaAllocatorMode.Contiguous)
            {
                ((ContiguousArenaAllocator*)arena)->Reallocate(ref refData, requiredCount);
            }
            else
            {
                ((ArenaAllocator*)arena)->Reallocate(ref refData, requiredCount);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte* Resolve(IntPtr arena, ArenaAllocatorMode mode, in ArenaBufferRefData refData)
        {
            return mode == ArenaAllocatorMode.Contiguous
                ? ((ContiguousArenaAllocator*)arena)->Resolve(refData.Handle)
                : (byte*)refData.Block;
        }

        /// <summary>
        /// Releases a block. <paramref name="block"/> is the raw <see cref="ArenaBufferRefData.Block"/> field,
        /// which the Paged branch reads as an address and every other branch reads as a packed handle.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Free(IntPtr arena, ArenaAllocatorMode mode, IntPtr block, int capacity, int elementSize)
        {
            if (mode == ArenaAllocatorMode.Contiguous)
            {
                ((ContiguousArenaAllocator*)arena)->Free((int)block, capacity);
            }
            else
            {
                ((ArenaAllocator*)arena)->Free((byte*)block, capacity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LiveBlocks(IntPtr arena, ArenaAllocatorMode mode)
        {
            switch (mode)
            {
                case ArenaAllocatorMode.Contiguous:
                    return ((ContiguousArenaAllocator*)arena)->LiveBlocks;
                default:
                    return ((ArenaAllocator*)arena)->LiveBlocks;
            }
        }

        public static void Reset(IntPtr arena, ArenaAllocatorMode mode)
        {
            if (mode == ArenaAllocatorMode.Contiguous)
            {
                ((ContiguousArenaAllocator*)arena)->Reset();
            }
            else
            {
                ((ArenaAllocator*)arena)->Reset();
            }
        }

        public static void Destroy(IntPtr arena, ArenaAllocatorMode mode)
        {
            if (mode == ArenaAllocatorMode.Contiguous)
            {
                ContiguousArenaAllocator.Destroy((ContiguousArenaAllocator*)arena);
            }
            else
            {
                ArenaAllocator.Destroy((ArenaAllocator*)arena);
            }
        }
    }
}
