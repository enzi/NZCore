// <copyright project="NZCore" file="ChunkPagedArenaAllocator.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NZCore
{
    /// <summary>
    /// A paged allocator that hands a whole entity chunk its own page, so chunk iteration resolves the page
    /// once for the chunk instead of once per entity.
    ///
    /// Block management is the same as <see cref="ArenaAllocator"/> - power of two size classes, free lists
    /// threaded through the free blocks - and so is the handle encoding, a page index packed above a byte
    /// offset. Two things differ:
    ///
    /// Pages are variable sized and allocated per reservation rather than fixed at 64 KB. When the reserve
    /// system reserves a chunk it calls <see cref="AllocateChunk"/> once, which takes a page big enough for
    /// the whole chunk at a single size class and hands out consecutive blocks from it. Every record in that
    /// chunk then carries the same page index, which is what lets
    /// <see cref="ChunkPagedArenaBuffer{T}"/> skip the page table load.
    ///
    /// A chunk's page uses one size class for every buffer in it, chosen from the largest capacity requested
    /// in the chunk. Buffers that asked for less are padded to it. That is the cost of the mode: a chunk
    /// holding one large buffer and many small ones wastes the difference on every small one.
    ///
    /// None of this is an invariant the allocator can enforce. An entity that moves to another chunk takes its
    /// record but not its block, and a buffer that outgrows its class is reallocated out of the chunk's page
    /// into an ordinary free list block. Both are handled by the buffer checking the page index rather than
    /// assuming it, so they cost a predictable branch rather than correctness.
    /// </summary>
    public unsafe struct ChunkPagedArenaAllocator : IDisposable
    {
        /// <summary>
        /// Bits of a handle used for the byte offset inside a page, so a page tops out at 1 MB. Larger than
        /// <see cref="ArenaAllocator.PageShift"/> because a page here has to hold a whole chunk, not a fixed
        /// 64 KB, and smaller than a full chunk of large buffers would need - see <see cref="MaxPageBytes"/>.
        /// </summary>
        public const int PageShift = 20;

        public const int MaxPageBytes = 1 << PageShift;

        private const int PageOffsetMask = MaxPageBytes - 1;

        /// <summary>Keeps every handle positive, so -1 stays available as "unreserved".</summary>
        public const int MaxPages = 1 << 11;

        /// <summary>Power of two size classes, covering blocks of 1 to 2^30 elements.</summary>
        public const int SizeClassCount = 31;

        /// <summary>A free block stores the next handle in its first four bytes, so it cannot be smaller.</summary>
        private const int MinBlockBytes = 4;

        [NativeDisableUnsafePtrRestriction] [NoAlias]
        private byte** _pages;

        [NativeDisableUnsafePtrRestriction]
        private int* _freeHeads;

        private int _pageCount;
        private int _pageTableCapacity;

        private int _elementSize;
        private int _blockAlign;

        private int _liveBlocks;
        private int _usedElements;
        private long _allocatedBytes;

        /// <summary>Size of a single element in bytes.</summary>
        public readonly int ElementSize => _elementSize;

        /// <summary>Pages currently allocated. Never decreases except on <see cref="Reset"/>.</summary>
        public readonly int PageCount => _pageCount;

        /// <summary>Total bytes held by the pages, including free blocks and page tail waste.</summary>
        public readonly long AllocatedBytes => _allocatedBytes;

        /// <summary>Sum of the capacities of all live blocks. Excludes blocks sitting on a free list.</summary>
        public readonly int UsedElements => _usedElements;

        /// <summary>
        /// Blocks handed out and not yet freed, across every World sharing this arena. The reserve system
        /// compares this against the number of live <c>*Ref</c> components to spot leaked blocks.
        /// </summary>
        public readonly int LiveBlocks => _liveBlocks;

        public readonly bool IsCreated => _freeHeads != null;

        public static ChunkPagedArenaAllocator* Create(int elementSize, int alignOf, int initialCapacity)
        {
            var arena = (ChunkPagedArenaAllocator*)Memory.Unmanaged.Allocate(
                UnsafeUtility.SizeOf<ChunkPagedArenaAllocator>(), UnsafeUtility.AlignOf<ChunkPagedArenaAllocator>(), Allocator.Persistent);
            *arena = default;

            arena->_elementSize = elementSize;
            arena->_blockAlign = math.max(alignOf, MinBlockBytes);

            arena->_freeHeads = (int*)Memory.Unmanaged.Allocate(sizeof(int) * SizeClassCount, UnsafeUtility.AlignOf<int>(), Allocator.Persistent);
            for (var i = 0; i < SizeClassCount; i++)
            {
                arena->_freeHeads[i] = ArenaBufferRefData.Unreserved;
            }

            arena->_pageTableCapacity = 8;
            arena->_pages = (byte**)Memory.Unmanaged.Allocate(sizeof(byte*) * arena->_pageTableCapacity, UnsafeUtility.AlignOf<IntPtr>(), Allocator.Persistent);
            arena->_pageCount = 0;

            // Unlike the other allocators there is nothing useful to pre-carve here: pages are sized to the
            // chunk that asks for one, so a page carved before any chunk exists would be the wrong size.
            return arena;
        }

        public static void Destroy(ChunkPagedArenaAllocator* arena)
        {
            if (arena == null)
            {
                return;
            }

            arena->Dispose();
            Memory.Unmanaged.Free(arena, Allocator.Persistent);
        }

        public void Dispose()
        {
            FreeAllPages();

            if (_pages != null)
            {
                Memory.Unmanaged.Free(_pages, Allocator.Persistent);
                _pages = null;
            }

            if (_freeHeads != null)
            {
                Memory.Unmanaged.Free(_freeHeads, Allocator.Persistent);
                _freeHeads = null;
            }

            _pageTableCapacity = 0;
        }

        /// <summary>
        /// Releases every page and empties the free lists. Only valid when no entity still references a
        /// block - it exists so tests can start from a clean arena, since arenas are shared between Worlds.
        /// </summary>
        public void Reset()
        {
            FreeAllPages();

            for (var i = 0; i < SizeClassCount; i++)
            {
                _freeHeads[i] = ArenaBufferRefData.Unreserved;
            }
        }

        /// <summary>Smallest size class that can hold <paramref name="elementCount"/> elements.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeClassOf(int elementCount)
        {
            return ArenaAllocator.SizeClassOf(elementCount);
        }

        /// <summary>Rounds an element count up to the capacity the arena would actually hand out for it.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RoundCapacity(int elementCount)
        {
            return ArenaAllocator.RoundCapacity(elementCount);
        }

        /// <summary>Resolves a block handle to its address. Pages never move, so the result stays valid.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly byte* Resolve(int handle)
        {
            return _pages[handle >> PageShift] + (handle & PageOffsetMask);
        }

        /// <summary>Base address of a page, for callers that resolve a whole chunk's worth of offsets.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly byte* ResolvePage(int pageIndex)
        {
            return _pages[pageIndex];
        }

        /// <summary>
        /// Reserves <paramref name="blockCount"/> consecutive blocks, all of the size class that holds
        /// <paramref name="elementCount"/> elements, on a page of their own. Returns the handle of the first;
        /// the rest follow at <see cref="BlockStride"/> intervals, so the caller walks them without asking
        /// again. This is the call that gives a chunk one page.
        ///
        /// A chunk too large to fit <see cref="MaxPageBytes"/> is split over consecutive pages, which the
        /// buffer's page check absorbs - it simply reloads at each boundary instead of once per chunk.
        /// </summary>
        public int AllocateChunk(int blockCount, int elementCount, out int blocksOnFirstPage)
        {
            var sizeClass = SizeClassOf(elementCount);
            CheckSizeClass(sizeClass);
            CheckBlockCount(blockCount);

            var stride = BlockStride(sizeClass);
            var perPage = math.max(1, MaxPageBytes / stride);
            var onThisPage = math.min(blockCount, perPage);

            var firstHandle = CarveDedicatedPage(onThisPage, stride);

            _liveBlocks += onThisPage;
            _usedElements += onThisPage * (1 << sizeClass);

            blocksOnFirstPage = onThisPage;
            return firstHandle;
        }

        /// <summary>
        /// Takes a block able to hold <paramref name="elementCount"/> elements off its size class free list,
        /// carving a page first if that list is empty. Used for entities that arrive after their chunk was
        /// reserved, and for buffers that outgrow the class their chunk page was carved at - both of which
        /// leave the block outside the chunk's page.
        /// </summary>
        public int Allocate(int elementCount)
        {
            var sizeClass = SizeClassOf(elementCount);
            CheckSizeClass(sizeClass);

            if (_freeHeads[sizeClass] == ArenaBufferRefData.Unreserved)
            {
                CarveSharedPage(sizeClass);
            }

            var handle = _freeHeads[sizeClass];

            // The free block's own first four bytes are the chain link.
            _freeHeads[sizeClass] = *(int*)Resolve(handle);

            _liveBlocks++;
            _usedElements += 1 << sizeClass;

            return handle;
        }

        /// <summary>
        /// Pushes a block back onto the free list of its size class. <paramref name="capacity"/> must be the
        /// capacity the block was handed out with. Blocks from a chunk page come back the same way, which is
        /// what lets a destroyed chunk's page be reused piecemeal rather than only as a whole.
        /// </summary>
        public void Free(int handle, int capacity)
        {
            // A capacity of zero means no block was ever handed out - freeing it would corrupt the size
            // class 0 chain and drive the counters negative.
            if (handle == ArenaBufferRefData.Unreserved || capacity <= 0)
            {
                return;
            }

            var sizeClass = SizeClassOf(capacity);
            CheckSizeClass(sizeClass);
            CheckFreedCapacity(capacity);

            *(int*)Resolve(handle) = _freeHeads[sizeClass];
            _freeHeads[sizeClass] = handle;

            _liveBlocks--;
            _usedElements -= 1 << sizeClass;
        }

        /// <summary>
        /// Moves a block into one large enough for <paramref name="requiredCount"/> elements, copying the
        /// live elements across and freeing the old block. Writes the new handle and capacity back into
        /// <paramref name="refData"/>. No-op when the block is already the right size class.
        ///
        /// The new block comes off a shared free list, not the chunk's page, so a buffer that grows leaves
        /// its chunk's page behind and costs its chunk one extra page load per iteration afterwards.
        /// </summary>
        public void Reallocate(ref ArenaBufferRefData refData, int requiredCount)
        {
            var newCapacity = RoundCapacity(requiredCount);
            if (refData.IsReserved && newCapacity == refData.Capacity)
            {
                return;
            }

            var oldHandle = refData.Handle;
            var oldCapacity = refData.Capacity;

            var newHandle = Allocate(requiredCount);

            if (refData.IsReserved && refData.Length > 0)
            {
                var copyCount = math.min(refData.Length, newCapacity);
                UnsafeUtility.MemCpy(Resolve(newHandle), Resolve(oldHandle), (long)copyCount * _elementSize);
            }

            Free(oldHandle, oldCapacity);

            refData.Handle = newHandle;
            refData.Capacity = newCapacity;
            refData.Length = math.min(refData.Length, newCapacity);
        }

        /// <summary>Blocks sitting on a size class free list. Walks the chain, so tests and diagnostics only.</summary>
        public readonly int FreeBlockCount(int sizeClass)
        {
            CheckSizeClass(sizeClass);

            var count = 0;
            var handle = _freeHeads[sizeClass];

            while (handle != ArenaBufferRefData.Unreserved)
            {
                count++;
                handle = *(int*)Resolve(handle);
            }

            return count;
        }

        /// <summary>Bytes one block of a size class occupies, padded so it can hold a chain link and stay aligned.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int BlockStride(int sizeClass)
        {
            var blockBytes = math.max((1 << sizeClass) * _elementSize, MinBlockBytes);
            return (blockBytes + _blockAlign - 1) / _blockAlign * _blockAlign;
        }

        /// <summary>
        /// Adds a page holding exactly <paramref name="blockCount"/> blocks and hands them all out live,
        /// without touching a free list. Returns the first block's handle.
        /// </summary>
        private int CarveDedicatedPage(int blockCount, int stride)
        {
            var pageBytes = (long)blockCount * stride;
            var pageIndex = AddPage(pageBytes, blockCount);

            return pageIndex << PageShift;
        }

        /// <summary>
        /// Adds a page for a size class and threads all of its blocks onto that class's free list. This is
        /// the ordinary paged behaviour, used for blocks that are not part of a chunk reservation.
        /// </summary>
        private void CarveSharedPage(int sizeClass)
        {
            var stride = BlockStride(sizeClass);
            var pageBytes = math.max(stride, MaxPageBytes);
            var blocks = pageBytes / stride;

            var pageIndex = AddPage(pageBytes, blocks);
            var page = _pages[pageIndex];
            var head = _freeHeads[sizeClass];

            // Walk backwards so the list comes out ascending, which keeps consecutive allocations
            // consecutive in memory - the locality the arena exists for.
            for (var b = blocks - 1; b >= 0; b--)
            {
                var byteOffset = b * stride;
                var handle = (pageIndex << PageShift) | byteOffset;

                *(int*)(page + byteOffset) = head;
                head = handle;
            }

            _freeHeads[sizeClass] = head;
        }

        private int AddPage(long pageBytes, int blockCount)
        {
            CheckPageBudget();
            CheckPageOffsets(pageBytes, blockCount);

            if (_pageCount == _pageTableCapacity)
            {
                GrowPageTable();
            }

            var pageIndex = _pageCount;

            _pages[pageIndex] = (byte*)Memory.Unmanaged.Allocate(pageBytes, _blockAlign, Allocator.Persistent);
            _pageCount++;
            _allocatedBytes += pageBytes;

            return pageIndex;
        }

        /// <summary>
        /// Only the pointer table is reallocated. The pages themselves never move, which is what keeps live
        /// handles and resolved addresses valid across a growth.
        /// </summary>
        private void GrowPageTable()
        {
            var newCapacity = _pageTableCapacity * 2;
            var newPages = (byte**)Memory.Unmanaged.Allocate(sizeof(byte*) * newCapacity, UnsafeUtility.AlignOf<IntPtr>(), Allocator.Persistent);

            UnsafeUtility.MemCpy(newPages, _pages, (long)sizeof(byte*) * _pageCount);
            Memory.Unmanaged.Free(_pages, Allocator.Persistent);

            _pages = newPages;
            _pageTableCapacity = newCapacity;
        }

        private void FreeAllPages()
        {
            for (var i = 0; i < _pageCount; i++)
            {
                Memory.Unmanaged.Free(_pages[i], Allocator.Persistent);
                _pages[i] = null;
            }

            _pageCount = 0;
            _allocatedBytes = 0;
            _liveBlocks = 0;
            _usedElements = 0;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckSizeClass(int sizeClass)
        {
            if ((uint)sizeClass >= SizeClassCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sizeClass),
                    $"Arena block size class {sizeClass} is out of range. The largest supported block is {1 << (SizeClassCount - 1)} elements.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckBlockCount(int blockCount)
        {
            if (blockCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blockCount), $"A chunk reservation needs at least one block, got {blockCount}.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckFreedCapacity(int capacity)
        {
            if (capacity != ArenaAllocator.RoundCapacity(capacity))
            {
                throw new ArgumentException(
                    $"Freed a block with capacity {capacity}, which is not a power of two. Only capacities the arena handed out may be freed.", nameof(capacity));
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private readonly void CheckPageBudget()
        {
            if (_pageCount >= MaxPages)
            {
                throw new InvalidOperationException(
                    $"Chunk paged arena for a {_elementSize} byte element type ran out of pages ({MaxPages}). A page is taken per reserved chunk, so this mode suits element types with few chunks - use ArenaAllocatorMode.Paged or Contiguous instead.");
            }
        }

        /// <summary>
        /// Only offsets have to fit the encoding, not the page. A page holding a single block puts it at
        /// offset zero, so one block may be larger than <see cref="MaxPageBytes"/>; two or more may not,
        /// because the second one's offset would overflow into the page index.
        /// </summary>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckPageOffsets(long pageBytes, int blockCount)
        {
            if (blockCount > 1 && pageBytes > MaxPageBytes)
            {
                throw new InvalidOperationException(
                    $"Chunk paged arena tried to lay {blockCount} blocks over {pageBytes} bytes, past the {MaxPageBytes} byte offset limit the handle encoding allows.");
            }
        }
    }
}
