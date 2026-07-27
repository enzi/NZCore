// <copyright project="NZCore" file="ArenaAllocator.cs">
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
    /// The paged block allocator behind every <see cref="ArenaBuffer{T}"/> of one element type.
    ///
    /// Memory comes in fixed size pages. A page is carved into equal blocks of a single power of two size
    /// class, and the free blocks of a class are threaded into a chain that lives <b>inside the free blocks
    /// themselves</b> - a freed block's first eight bytes hold the next free block's address, so there is no
    /// side table and freeing is a two store push.
    ///
    /// Pages are never moved or reallocated, which is the property the whole design leans on: growing one
    /// buffer can never invalidate another buffer's memory. Only the buffer being grown moves, because it is
    /// copied into a block of the next size class.
    ///
    /// Because pages never move, blocks are addressed by their <b>address</b> rather than by a packed handle.
    /// That is what makes reaching a buffer's elements free: the address sits in the record in chunk memory
    /// that the caller is already reading, so there is no page table load and nothing to decode. The paged
    /// allocator is the only mode that can do this - the others move blocks, so they must stay on handles.
    /// The cost is four extra bytes per record, since <see cref="ArenaBufferRefData.Block"/> has to be
    /// pointer wide.
    /// </summary>
    public unsafe struct ArenaAllocator : IDisposable
    {
        /// <summary>
        /// Bits of a handle used for the byte offset inside a page, so a page is 1 MB.
        ///
        /// Raised from 16 (64 KB) as an experiment: across three measured page sizes the multi type read
        /// throughput moved monotonically with page size - 16 KB scored 0.45x, 64 KB 1.00x and 1 MB 1.44x
        /// against 64 KB paged - which points at page size, rather than any per chunk grouping, as what the
        /// ChunkPaged and SharedChunkPaged modes were actually buying.
        /// </summary>
        public const int PageShift = 20;

        public const int PageSizeBytes = 1 << PageShift;

        /// <summary>
        /// Ceiling on the page table, and so on the arena at 1 MB a page. Addressing no longer constrains
        /// this - blocks are reached by address, not by a handle with a fixed bit budget - so it is now a
        /// budget check rather than a hard encoding limit, and can be raised if an arena legitimately needs
        /// more than 2 GB.
        /// </summary>
        public const int MaxPages = 1 << 11;

        /// <summary>Power of two size classes, covering blocks of 1 to 2^30 elements.</summary>
        public const int SizeClassCount = 31;

        /// <summary>A free block stores the next block's address in its first eight bytes, so it cannot be smaller.</summary>
        private const int MinBlockBytes = 8;

        [NativeDisableUnsafePtrRestriction] [NoAlias]
        private byte** _pages;

        /// <summary>Head of each size class's free chain. Null means the class has no free block left.</summary>
        [NativeDisableUnsafePtrRestriction]
        private byte** _freeHeads;

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

        public static ArenaAllocator* Create(int elementSize, int alignOf, int initialCapacity)
        {
            var arena = (ArenaAllocator*)Memory.Unmanaged.Allocate(
                UnsafeUtility.SizeOf<ArenaAllocator>(), UnsafeUtility.AlignOf<ArenaAllocator>(), Allocator.Persistent);
            *arena = default;

            arena->_elementSize = elementSize;
            arena->_blockAlign = math.max(alignOf, MinBlockBytes);

            arena->_freeHeads = (byte**)Memory.Unmanaged.Allocate(sizeof(byte*) * SizeClassCount, UnsafeUtility.AlignOf<IntPtr>(), Allocator.Persistent);
            for (var i = 0; i < SizeClassCount; i++)
            {
                arena->_freeHeads[i] = null;
            }

            arena->_pageTableCapacity = 8;
            arena->_pages = (byte**)Memory.Unmanaged.Allocate(sizeof(byte*) * arena->_pageTableCapacity, UnsafeUtility.AlignOf<IntPtr>(), Allocator.Persistent);
            arena->_pageCount = 0;

            // A hint about the typical buffer size for this element type. Carving one page of that class up
            // front means the first reservations do not pay for a page allocation. Zero means carve lazily,
            // which is the right default - pre-carving the wrong class just wastes a page.
            if (initialCapacity > 0)
            {
                arena->CarvePage(SizeClassOf(initialCapacity));
            }

            return arena;
        }

        public static void Destroy(ArenaAllocator* arena)
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
                _freeHeads[i] = null;
            }
        }

        /// <summary>Smallest size class that can hold <paramref name="elementCount"/> elements.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeClassOf(int elementCount)
        {
            return math.tzcnt(math.ceilpow2(math.max(1, elementCount)));
        }

        /// <summary>Rounds an element count up to the capacity the arena would actually hand out for it.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RoundCapacity(int elementCount)
        {
            return math.ceilpow2(math.max(1, elementCount));
        }

        /// <summary>
        /// Takes a block able to hold <paramref name="elementCount"/> elements off its size class free list,
        /// carving a new page first if that list is empty. The block's real capacity is
        /// <see cref="RoundCapacity"/> of the requested count.
        /// </summary>
        public byte* Allocate(int elementCount)
        {
            var sizeClass = SizeClassOf(elementCount);
            CheckSizeClass(sizeClass);

            if (_freeHeads[sizeClass] == null)
            {
                CarvePage(sizeClass);
            }

            var block = _freeHeads[sizeClass];

            // The free block's own first eight bytes are the chain link.
            _freeHeads[sizeClass] = *(byte**)block;

            _liveBlocks++;
            _usedElements += 1 << sizeClass;

            return block;
        }

        /// <summary>
        /// Pushes a block back onto the free list of its size class. <paramref name="capacity"/> must be the
        /// capacity the block was handed out with.
        /// </summary>
        public void Free(byte* block, int capacity)
        {
            // A capacity of zero means no block was ever handed out - freeing it would corrupt the size
            // class 0 chain and drive the counters negative. The unreserved sentinel is all-ones rather than
            // null, so both readings of an empty record have to be rejected here.
            if (block == null || block == (byte*)ArenaBufferRefData.Unreserved || capacity <= 0)
            {
                return;
            }

            var sizeClass = SizeClassOf(capacity);
            CheckSizeClass(sizeClass);
            CheckFreedCapacity(capacity);

            *(byte**)block = _freeHeads[sizeClass];
            _freeHeads[sizeClass] = block;

            _liveBlocks--;
            _usedElements -= 1 << sizeClass;
        }

        /// <summary>
        /// Moves a block into one large enough for <paramref name="requiredCount"/> elements, copying the
        /// live elements across and freeing the old block. Writes the new handle and capacity back into
        /// <paramref name="refData"/>. No-op when the block is already the right size class.
        /// </summary>
        public void Reallocate(ref ArenaBufferRefData refData, int requiredCount)
        {
            var newCapacity = RoundCapacity(requiredCount);
            if (refData.IsReserved && newCapacity == refData.Capacity)
            {
                return;
            }

            var oldBlock = (byte*)refData.Block;
            var oldCapacity = refData.Capacity;
            var wasReserved = refData.IsReserved;

            var newBlock = Allocate(requiredCount);

            if (wasReserved && refData.Length > 0)
            {
                var copyCount = math.min(refData.Length, newCapacity);
                UnsafeUtility.MemCpy(newBlock, oldBlock, (long)copyCount * _elementSize);
            }

            if (wasReserved)
            {
                Free(oldBlock, oldCapacity);
            }

            refData.Block = (IntPtr)newBlock;
            refData.Capacity = newCapacity;
            refData.Length = math.min(refData.Length, newCapacity);
        }

        /// <summary>Blocks sitting on a size class free list. Walks the chain, so tests and diagnostics only.</summary>
        public readonly int FreeBlockCount(int sizeClass)
        {
            CheckSizeClass(sizeClass);

            var count = 0;
            var block = _freeHeads[sizeClass];

            while (block != null)
            {
                count++;
                block = *(byte**)block;
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

        /// <summary>Adds a page for a size class and threads all of its blocks onto that class's free list.</summary>
        private void CarvePage(int sizeClass)
        {
            var stride = BlockStride(sizeClass);

            // A block bigger than a page gets a page of its own, sized to fit exactly one.
            var pageBytes = math.max(stride, PageSizeBytes);

            CheckPageBudget();

            if (_pageCount == _pageTableCapacity)
            {
                GrowPageTable();
            }

            var page = (byte*)Memory.Unmanaged.Allocate(pageBytes, _blockAlign, Allocator.Persistent);
            var pageIndex = _pageCount;

            _pages[pageIndex] = page;
            _pageCount++;
            _allocatedBytes += pageBytes;

            var blocks = pageBytes / stride;
            var head = _freeHeads[sizeClass];

            // Walk backwards so the list comes out ascending, which keeps consecutive allocations
            // consecutive in memory - the locality the arena exists for.
            for (var b = blocks - 1; b >= 0; b--)
            {
                var block = page + b * stride;

                *(byte**)block = head;
                head = block;
            }

            _freeHeads[sizeClass] = head;
        }

        private void GrowPageTable()
        {
            var newCapacity = _pageTableCapacity * 2;
            var newTable = (byte**)Memory.Unmanaged.Allocate(sizeof(byte*) * newCapacity, UnsafeUtility.AlignOf<IntPtr>(), Allocator.Persistent);

            // Only the table of pointers moves. The pages it points at do not, which is why handles taken
            // before this call stay valid.
            UnsafeUtility.MemCpy(newTable, _pages, sizeof(byte*) * (long)_pageCount);
            Memory.Unmanaged.Free(_pages, Allocator.Persistent);

            _pages = newTable;
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
            _liveBlocks = 0;
            _usedElements = 0;
            _allocatedBytes = 0;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private readonly void CheckSizeClass(int sizeClass)
        {
            if ((uint)sizeClass >= SizeClassCount)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeClass),
                    $"Arena block size class {sizeClass} is out of range. The largest supported block is {1 << (SizeClassCount - 1)} elements.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private readonly void CheckFreedCapacity(int capacity)
        {
            if (capacity != math.ceilpow2(capacity))
            {
                throw new ArgumentException($"Arena block capacity {capacity} is not a power of two, so it was never handed out by this allocator.", nameof(capacity));
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private readonly void CheckPageBudget()
        {
            if (_pageCount >= MaxPages)
            {
                throw new InvalidOperationException(
                    $"Arena for a {_elementSize} byte element type ran out of pages ({MaxPages} x {PageSizeBytes} bytes). " +
                    "Handles pack the page index into the high bits of an int, so the page count is capped.");
            }
        }
    }
}
