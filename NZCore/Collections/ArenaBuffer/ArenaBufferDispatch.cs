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
            if (mode == ArenaAllocatorMode.SharedChunkPaged)
            {
                ((SharedArenaAllocator*)arena)->Reallocate(ref refData, requiredCount, elementSize);
            }
            else if (mode == ArenaAllocatorMode.Contiguous)
            {
                ((ContiguousArenaAllocator*)arena)->Reallocate(ref refData, requiredCount);
            }
            else if (mode == ArenaAllocatorMode.ChunkPaged)
            {
                ((ChunkPagedArenaAllocator*)arena)->Reallocate(ref refData, requiredCount);
            }
            else
            {
                ((ArenaAllocator*)arena)->Reallocate(ref refData, requiredCount);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Free(IntPtr arena, ArenaAllocatorMode mode, int handle, int capacity, int elementSize)
        {
            if (mode == ArenaAllocatorMode.SharedChunkPaged)
            {
                // Byte oriented arena: the block size has to be rebuilt from the capacity and element size.
                ((SharedArenaAllocator*)arena)->Free(handle, SharedArenaAllocator.BlockBytes(capacity, elementSize));
            }
            else if (mode == ArenaAllocatorMode.Contiguous)
            {
                ((ContiguousArenaAllocator*)arena)->Free(handle, capacity);
            }
            else if (mode == ArenaAllocatorMode.ChunkPaged)
            {
                ((ChunkPagedArenaAllocator*)arena)->Free(handle, capacity);
            }
            else
            {
                ((ArenaAllocator*)arena)->Free(handle, capacity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LiveBlocks(IntPtr arena, ArenaAllocatorMode mode)
        {
            switch (mode)
            {
                case ArenaAllocatorMode.SharedChunkPaged:
                    return ((SharedArenaAllocator*)arena)->LiveBlocks;
                case ArenaAllocatorMode.Contiguous:
                    return ((ContiguousArenaAllocator*)arena)->LiveBlocks;
                case ArenaAllocatorMode.ChunkPaged:
                    return ((ChunkPagedArenaAllocator*)arena)->LiveBlocks;
                default:
                    return ((ArenaAllocator*)arena)->LiveBlocks;
            }
        }

        public static void Reset(IntPtr arena, ArenaAllocatorMode mode)
        {
            if (mode == ArenaAllocatorMode.SharedChunkPaged)
            {
                ((SharedArenaAllocator*)arena)->Reset();
            }
            else if (mode == ArenaAllocatorMode.Contiguous)
            {
                ((ContiguousArenaAllocator*)arena)->Reset();
            }
            else if (mode == ArenaAllocatorMode.ChunkPaged)
            {
                ((ChunkPagedArenaAllocator*)arena)->Reset();
            }
            else
            {
                ((ArenaAllocator*)arena)->Reset();
            }
        }

        /// <summary>
        /// Reserves <paramref name="blockCount"/> consecutive blocks of one size class on a page of their own,
        /// returning the first handle and how many of the requested blocks landed on that page. Only
        /// <see cref="ArenaAllocatorMode.ChunkPaged"/> supports this; the reserve system checks the mode
        /// before calling rather than paying for a fallback here.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AllocateChunk(IntPtr arena, int blockCount, int elementCount, out int blocksOnFirstPage)
        {
            return ((ChunkPagedArenaAllocator*)arena)->AllocateChunk(blockCount, elementCount, out blocksOnFirstPage);
        }

        /// <summary>Block stride of a size class, so the reserve system can walk a chunk page's blocks.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ChunkPagedBlockStride(IntPtr arena, int elementCount)
        {
            return ((ChunkPagedArenaAllocator*)arena)->BlockStride(ChunkPagedArenaAllocator.SizeClassOf(elementCount));
        }

        public static void Destroy(IntPtr arena, ArenaAllocatorMode mode)
        {
            if (mode == ArenaAllocatorMode.SharedChunkPaged)
            {
                SharedArenaAllocator.Destroy((SharedArenaAllocator*)arena);
            }
            else if (mode == ArenaAllocatorMode.Contiguous)
            {
                ContiguousArenaAllocator.Destroy((ContiguousArenaAllocator*)arena);
            }
            else if (mode == ArenaAllocatorMode.ChunkPaged)
            {
                ChunkPagedArenaAllocator.Destroy((ChunkPagedArenaAllocator*)arena);
            }
            else
            {
                ArenaAllocator.Destroy((ArenaAllocator*)arena);
            }
        }
    }
}
