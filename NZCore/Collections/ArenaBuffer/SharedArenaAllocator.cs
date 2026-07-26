// <copyright project="NZCore" file="SharedArenaAllocator.cs">
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
    /// One arena serving every element type registered as <see cref="ArenaAllocatorMode.SharedChunkPaged"/>,
    /// which lets a chunk's buffers of <b>different</b> types share a page.
    ///
    /// The other allocators are one per element type and know their element size. This one cannot: the types
    /// sharing it have different element sizes, so it deals purely in bytes. Size classes are powers of two
    /// bytes, free lists are threaded through the free blocks as everywhere else, and converting an element
    /// count into bytes is the caller's job - which is why <see cref="Free"/> takes a byte size rather than a
    /// capacity, and why the registration has to carry each type's element size for the untyped release path.
    ///
    /// Pages are 16 KB and tiled, mirroring an entity chunk: each page holds as many of a chunk's entities as
    /// fit, and within a page each element type gets a contiguous run, one after another. Reading several types
    /// of the same entities therefore stays inside a page, while reading one type walks its run within each
    /// page in turn. That is the trade against a single huge page per chunk, where one type's whole run is
    /// contiguous but the types are far apart.
    ///
    /// As with <see cref="ChunkPagedArenaAllocator"/> none of this is an invariant. A buffer that grows takes
    /// an ordinary free list block and leaves its chunk's page; the buffer checks the page index rather than
    /// assuming it.
    /// </summary>
    public unsafe struct SharedArenaAllocator : IDisposable
    {
        /// <summary>
        /// Bits of a handle used for the byte offset inside a page, so a page is 16 KB - deliberately the same
        /// size as an entity chunk. A page holds a subset of a chunk's entities laid out the way components are
        /// laid out in a chunk: one contiguous run per element type, side by side. A read that touches several
        /// types of the same entities therefore stays inside one page.
        /// </summary>
        public const int PageShift = 14;

        public const int MaxPageBytes = 1 << PageShift;

        private const int PageOffsetMask = MaxPageBytes - 1;

        /// <summary>
        /// Keeps every handle positive, so -1 stays available as "unreserved". Far more pages than the other
        /// allocators need, because these pages are 16 KB rather than 1 MB.
        /// </summary>
        public const int MaxPages = 1 << 17;

        /// <summary>Byte size classes, covering blocks of 1 byte to 1 GB.</summary>
        public const int SizeClassCount = 31;

        /// <summary>A free block stores the next handle in its first four bytes, so it cannot be smaller.</summary>
        public const int MinBlockBytes = 4;

        /// <summary>Every block is aligned to this, which covers any element type the shared types use.</summary>
        public const int BlockAlign = 16;

        [NativeDisableUnsafePtrRestriction] [NoAlias]
        private byte** _pages;

        [NativeDisableUnsafePtrRestriction]
        private int* _freeHeads;

        [NativeDisableUnsafePtrRestriction]
        private long* _pageBytes;

        private int _pageCount;
        private int _pageTableCapacity;

        private int _liveBlocks;
        private long _usedBytes;
        private long _allocatedBytes;

        /// <summary>Pages currently allocated. Never decreases except on <see cref="Reset"/>.</summary>
        public readonly int PageCount => _pageCount;

        /// <summary>Total bytes held by the pages, including free blocks and page tail waste.</summary>
        public readonly long AllocatedBytes => _allocatedBytes;

        /// <summary>Sum of the byte sizes of all live blocks. Excludes blocks sitting on a free list.</summary>
        public readonly long UsedBytes => _usedBytes;

        /// <summary>
        /// Blocks handed out and not yet freed, across every type sharing this arena and every World. The
        /// reserve system compares this against the number of live records <b>summed over all the sharing
        /// types</b>, since one type's count alone means nothing here.
        /// </summary>
        public readonly int LiveBlocks => _liveBlocks;

        public readonly bool IsCreated => _freeHeads != null;

        public static SharedArenaAllocator* Create()
        {
            var arena = (SharedArenaAllocator*)Memory.Unmanaged.Allocate(
                UnsafeUtility.SizeOf<SharedArenaAllocator>(), UnsafeUtility.AlignOf<SharedArenaAllocator>(), Allocator.Persistent);
            *arena = default;

            arena->_freeHeads = (int*)Memory.Unmanaged.Allocate(sizeof(int) * SizeClassCount, UnsafeUtility.AlignOf<int>(), Allocator.Persistent);
            for (var i = 0; i < SizeClassCount; i++)
            {
                arena->_freeHeads[i] = ArenaBufferRefData.Unreserved;
            }

            arena->_pageTableCapacity = 8;
            arena->_pages = (byte**)Memory.Unmanaged.Allocate(sizeof(byte*) * arena->_pageTableCapacity, UnsafeUtility.AlignOf<IntPtr>(), Allocator.Persistent);
            arena->_pageBytes = (long*)Memory.Unmanaged.Allocate(sizeof(long) * arena->_pageTableCapacity, UnsafeUtility.AlignOf<long>(), Allocator.Persistent);
            arena->_pageCount = 0;

            return arena;
        }

        public static void Destroy(SharedArenaAllocator* arena)
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

            if (_pageBytes != null)
            {
                Memory.Unmanaged.Free(_pageBytes, Allocator.Persistent);
                _pageBytes = null;
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

        /// <summary>Bytes a block actually occupies: a power of two, never below a chain link.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlockBytes(int capacity, int elementSize)
        {
            return math.ceilpow2(math.max(MinBlockBytes, capacity * elementSize));
        }

        /// <summary>Smallest byte size class that can hold <paramref name="bytes"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ByteClassOf(int bytes)
        {
            return math.tzcnt(math.ceilpow2(math.max(MinBlockBytes, bytes)));
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
        /// Takes a page of <paramref name="pageBytes"/> for a chunk to lay its types out in, and returns the
        /// handle of its first byte. The caller places blocks itself and tells the arena how many it placed
        /// through <see cref="AccountChunkBlocks"/>, because only the caller knows how the types divide it up.
        /// </summary>
        public int AllocateChunkPage(int pageBytes, out int usableBytes)
        {
            CheckChunkPageBytes(pageBytes);

            var pageIndex = AddPage(pageBytes);
            usableBytes = pageBytes;

            return pageIndex << PageShift;
        }

        /// <summary>
        /// Records blocks the caller laid out in a page it took from <see cref="AllocateChunkPage"/>. Keeps
        /// the live block count and used bytes honest without the arena having to know the type layout.
        /// </summary>
        public void AccountChunkBlocks(int blockCount, int blockBytes)
        {
            _liveBlocks += blockCount;
            _usedBytes += (long)blockCount * blockBytes;
        }

        /// <summary>
        /// Takes a block of at least <paramref name="bytes"/> off its size class free list, carving a page
        /// first if that list is empty. Used for buffers that outgrew the class their chunk page was laid out
        /// at, which leaves them outside that page.
        /// </summary>
        public int Allocate(int bytes)
        {
            var sizeClass = ByteClassOf(bytes);
            CheckSizeClass(sizeClass);

            if (_freeHeads[sizeClass] == ArenaBufferRefData.Unreserved)
            {
                CarveSharedPage(sizeClass);
            }

            var handle = _freeHeads[sizeClass];

            // The free block's own first four bytes are the chain link.
            _freeHeads[sizeClass] = *(int*)Resolve(handle);

            _liveBlocks++;
            _usedBytes += 1 << sizeClass;

            return handle;
        }

        /// <summary>
        /// Pushes a block back onto the free list of its byte size class. <paramref name="bytes"/> must be the
        /// block size it was handed out with - <see cref="BlockBytes"/> of the record's capacity and its
        /// type's element size.
        /// </summary>
        public void Free(int handle, int bytes)
        {
            if (handle == ArenaBufferRefData.Unreserved || bytes <= 0)
            {
                return;
            }

            var sizeClass = ByteClassOf(bytes);
            CheckSizeClass(sizeClass);

            *(int*)Resolve(handle) = _freeHeads[sizeClass];
            _freeHeads[sizeClass] = handle;

            _liveBlocks--;
            _usedBytes -= 1 << sizeClass;
        }

        /// <summary>
        /// Moves a block into one large enough for <paramref name="requiredCount"/> elements of
        /// <paramref name="elementSize"/> bytes, copying the live elements across and freeing the old block.
        ///
        /// The new block comes off a shared free list rather than the chunk's page, so a buffer that grows
        /// leaves its chunk's page behind.
        /// </summary>
        public void Reallocate(ref ArenaBufferRefData refData, int requiredCount, int elementSize)
        {
            var newCapacity = ArenaAllocator.RoundCapacity(requiredCount);
            if (refData.IsReserved && newCapacity == refData.Capacity)
            {
                return;
            }

            var oldHandle = refData.Handle;
            var oldBytes = refData.IsReserved ? BlockBytes(refData.Capacity, elementSize) : 0;

            var newHandle = Allocate(BlockBytes(newCapacity, elementSize));

            if (refData.IsReserved && refData.Length > 0)
            {
                var copyCount = math.min(refData.Length, newCapacity);
                UnsafeUtility.MemCpy(Resolve(newHandle), Resolve(oldHandle), (long)copyCount * elementSize);
            }

            if (oldBytes > 0)
            {
                Free(oldHandle, oldBytes);
            }

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

        /// <summary>Adds a page for a byte size class and threads all of its blocks onto that class's list.</summary>
        private void CarveSharedPage(int sizeClass)
        {
            var stride = math.max(1 << sizeClass, MinBlockBytes);
            var pageBytes = math.max(stride, MaxPageBytes);
            var blocks = pageBytes / stride;

            var pageIndex = AddPage(pageBytes);
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

        private int AddPage(long pageBytes)
        {
            CheckPageBudget();

            if (_pageCount == _pageTableCapacity)
            {
                GrowPageTable();
            }

            var pageIndex = _pageCount;

            _pages[pageIndex] = (byte*)Memory.Unmanaged.Allocate(pageBytes, BlockAlign, Allocator.Persistent);
            _pageBytes[pageIndex] = pageBytes;
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
            var newBytes = (long*)Memory.Unmanaged.Allocate(sizeof(long) * newCapacity, UnsafeUtility.AlignOf<long>(), Allocator.Persistent);
            UnsafeUtility.MemCpy(newPages, _pages, (long)sizeof(byte*) * _pageCount);
            UnsafeUtility.MemCpy(newBytes, _pageBytes, (long)sizeof(long) * _pageCount);

            Memory.Unmanaged.Free(_pages, Allocator.Persistent);
            Memory.Unmanaged.Free(_pageBytes, Allocator.Persistent);

            _pages = newPages;
            _pageBytes = newBytes;
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
            _usedBytes = 0;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckSizeClass(int sizeClass)
        {
            if ((uint)sizeClass >= SizeClassCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sizeClass), $"Shared arena block size class {sizeClass} is out of range. The largest supported block is {1 << (SizeClassCount - 1)} bytes.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckChunkPageBytes(int pageBytes)
        {
            if (pageBytes <= 0 || pageBytes > MaxPageBytes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pageBytes), $"A shared chunk page must be between 1 and {MaxPageBytes} bytes, got {pageBytes}. Offsets past that would overflow into the page index.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private readonly void CheckPageBudget()
        {
            if (_pageCount >= MaxPages)
            {
                throw new InvalidOperationException(
                    $"Shared arena ran out of pages ({MaxPages}). A page is taken per reserved chunk, so this mode suits element types with few chunks - use ArenaAllocatorMode.Paged or Contiguous instead.");
            }
        }
    }
}
