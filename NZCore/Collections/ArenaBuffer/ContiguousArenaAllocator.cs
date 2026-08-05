// <copyright project="NZCore" file="ContiguousArenaAllocator.cs">
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
    /// The single block allocator behind every <see cref="ContiguousArenaBuffer{T}"/> of one element type.
    ///
    /// Block management is identical to <see cref="ArenaAllocator"/>: power of two size classes, and the free
    /// blocks of a class threaded into a chain that lives inside the free blocks themselves. What differs is
    /// where blocks come from. Instead of carving a fresh page per size class, this allocator bump carves runs
    /// out of the tail of one contiguous block, and reallocates that block when the tail runs out.
    ///
    /// The point of that is <see cref="Resolve"/>: a handle is a plain byte offset, so resolving is an add
    /// rather than a page table lookup.
    ///
    /// The cost is pointer lifetime. A reallocation moves everything, so any address obtained from this arena
    /// dies when <b>any</b> buffer of this element type grows past the block's tail - not just the one being
    /// grown. Handles survive, and so do the free lists, because both store offsets rather than addresses.
    /// </summary>
    public unsafe struct ContiguousArenaAllocator : IDisposable
    {
        /// <summary>Power of two size classes, covering blocks of 1 to 2^30 elements.</summary>
        public const int SizeClassCount = 31;

        /// <summary>A free block stores the next handle in its first four bytes, so it cannot be smaller.</summary>
        private const int MinBlockBytes = 4;

        /// <summary>
        /// Bytes of tail taken per carve. Pinned to 64 KB rather than tracking
        /// <see cref="ArenaAllocator.PageSizeBytes"/>: that constant is being varied to measure the effect of
        /// page size on the paged allocator, and this one has to hold still for that comparison to mean
        /// anything. The two were equal by construction when both were 64 KB.
        /// </summary>
        private const int CarveBytes = 1 << 16;

        /// <summary>Backing bytes taken on the first carve, when no capacity hint was given.</summary>
        private const int InitialBytes = CarveBytes;

        /// <summary>Handles are signed offsets, so the block cannot exceed what one can address.</summary>
        public const long MaxBytes = int.MaxValue;

        [NativeDisableUnsafePtrRestriction] [NoAlias]
        private byte* _base;

        [NativeDisableUnsafePtrRestriction]
        private int* _freeHeads;

        private long _capacityBytes;
        private long _bumpBytes;

        private int _elementSize;
        private int _blockAlign;

        private int _liveBlocks;
        private int _usedElements;

        /// <summary>Size of a single element in bytes.</summary>
        public readonly int ElementSize => _elementSize;

        /// <summary>Bytes of the backing block, including free blocks and the uncarved tail.</summary>
        public readonly long AllocatedBytes => _capacityBytes;

        /// <summary>Bytes of the backing block already carved into blocks.</summary>
        public readonly long CarvedBytes => _bumpBytes;

        /// <summary>Sum of the capacities of all live blocks. Excludes blocks sitting on a free list.</summary>
        public readonly int UsedElements => _usedElements;

        /// <summary>
        /// Blocks handed out and not yet freed, across every World sharing this arena. The reserve system
        /// compares this against the number of live <c>*Ref</c> components to spot leaked blocks.
        /// </summary>
        public readonly int LiveBlocks => _liveBlocks;

        public readonly bool IsCreated => _freeHeads != null;

        public static ContiguousArenaAllocator* Create(int elementSize, int alignOf, int initialCapacity)
        {
            var arena = (ContiguousArenaAllocator*)Memory.Unmanaged.Allocate(
                UnsafeUtility.SizeOf<ContiguousArenaAllocator>(), UnsafeUtility.AlignOf<ContiguousArenaAllocator>(), Allocator.Persistent);
            *arena = default;

            arena->_elementSize = elementSize;
            arena->_blockAlign = math.max(alignOf, MinBlockBytes);

            arena->_freeHeads = (int*)Memory.Unmanaged.Allocate(sizeof(int) * SizeClassCount, UnsafeUtility.AlignOf<int>(), Allocator.Persistent);
            for (var i = 0; i < SizeClassCount; i++)
            {
                arena->_freeHeads[i] = ArenaBufferRefData.Unreserved;
            }

            // A capacity hint matters more here than in the paged allocator: bytes reserved up front are bytes
            // the block never has to move to get, and a move is the one thing that invalidates pointers. It
            // doubles as the same size class hint the paged allocator reads it as.
            if (initialCapacity > 0)
            {
                arena->EnsureBackingBytes((long)initialCapacity * elementSize);
                arena->Carve(SizeClassOf(initialCapacity));
            }

            return arena;
        }

        public static void Destroy(ContiguousArenaAllocator* arena)
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
            FreeBackingBlock();

            if (_freeHeads != null)
            {
                Memory.Unmanaged.Free(_freeHeads, Allocator.Persistent);
                _freeHeads = null;
            }
        }

        /// <summary>
        /// Releases the backing block and empties the free lists. Only valid when no entity still references a
        /// block - it exists so tests can start from a clean arena, since arenas are shared between Worlds.
        /// </summary>
        public void Reset()
        {
            FreeBackingBlock();

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

        /// <summary>
        /// Resolves a block handle to its address. Unlike the paged allocator this is a plain add, and unlike
        /// the paged allocator the result dies when the arena grows.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly byte* Resolve(int handle)
        {
            return _base + handle;
        }

        /// <summary>
        /// Takes a block able to hold <paramref name="elementCount"/> elements off its size class free list,
        /// carving a run out of the tail first if that list is empty. The block's real capacity is
        /// <see cref="RoundCapacity"/> of the requested count.
        /// </summary>
        public int Allocate(int elementCount)
        {
            var sizeClass = SizeClassOf(elementCount);
            CheckSizeClass(sizeClass);

            if (_freeHeads[sizeClass] == ArenaBufferRefData.Unreserved)
            {
                Carve(sizeClass);
            }

            var handle = _freeHeads[sizeClass];

            // The free block's own first four bytes are the chain link.
            _freeHeads[sizeClass] = *(int*)(_base + handle);

            _liveBlocks++;
            _usedElements += 1 << sizeClass;

            return handle;
        }

        /// <summary>
        /// Pushes a block back onto the free list of its size class. <paramref name="capacity"/> must be the
        /// capacity the block was handed out with.
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

            *(int*)(_base + handle) = _freeHeads[sizeClass];
            _freeHeads[sizeClass] = handle;

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

            var oldHandle = refData.Handle;
            var oldCapacity = refData.Capacity;

            var newHandle = Allocate(requiredCount);

            // Both addresses are resolved after Allocate on purpose: allocating may have moved the whole
            // arena, which would leave an address taken beforehand pointing into the freed block.
            if (refData.IsReserved && refData.Length > 0)
            {
                var copyCount = math.min(refData.Length, newCapacity);
                UnsafeUtility.MemCpy(_base + newHandle, _base + oldHandle, (long)copyCount * _elementSize);
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
                handle = *(int*)(_base + handle);
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

        /// <summary>Takes a run of blocks off the tail and threads them onto a size class's free list.</summary>
        private void Carve(int sizeClass)
        {
            var stride = BlockStride(sizeClass);

            // A block bigger than a carve run gets a run of its own, sized to fit exactly one.
            var blocks = math.max(1, CarveBytes / stride);
            var runBytes = (long)blocks * stride;

            EnsureBackingBytes(_bumpBytes + runBytes);

            var start = (int)_bumpBytes;
            _bumpBytes += runBytes;

            var head = _freeHeads[sizeClass];

            // Walk backwards so the list comes out ascending, which keeps consecutive allocations
            // consecutive in memory - the locality the arena exists for.
            for (var b = blocks - 1; b >= 0; b--)
            {
                var offset = start + b * stride;

                *(int*)(_base + offset) = head;
                head = offset;
            }

            _freeHeads[sizeClass] = head;
        }

        /// <summary>
        /// Grows the backing block to hold at least <paramref name="required"/> bytes, moving it if it has to.
        /// Live handles and the free list chains are offsets, so they survive the move untouched - every
        /// resolved address does not.
        /// </summary>
        private void EnsureBackingBytes(long required)
        {
            if (required <= _capacityBytes)
            {
                return;
            }

            // Unconditional rather than a Conditional check: clamping to MaxBytes when the request genuinely
            // does not fit would silently hand back a block too small to hold the carve that asked for it.
            if (required > MaxBytes)
            {
                ThrowBackingBudgetExceeded();
            }

            // Plain comparisons rather than math.max: these are longs, and the doubling has to be clamped
            // both from below by the request and from above by what a handle can address.
            var newCapacity = _capacityBytes * 2;

            if (newCapacity < InitialBytes)
            {
                newCapacity = InitialBytes;
            }

            if (newCapacity < required)
            {
                newCapacity = required;
            }

            if (newCapacity > MaxBytes)
            {
                newCapacity = MaxBytes;
            }

            var newBase = (byte*)Memory.Unmanaged.Allocate(newCapacity, _blockAlign, Allocator.Persistent);

            if (_base != null)
            {
                UnsafeUtility.MemCpy(newBase, _base, _bumpBytes);
                Memory.Unmanaged.Free(_base, Allocator.Persistent);
            }

            _base = newBase;
            _capacityBytes = newCapacity;
        }

        private void FreeBackingBlock()
        {
            if (_base != null)
            {
                Memory.Unmanaged.Free(_base, Allocator.Persistent);
                _base = null;
            }

            _capacityBytes = 0;
            _bumpBytes = 0;
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
        private static void CheckFreedCapacity(int capacity)
        {
            if (capacity != ArenaAllocator.RoundCapacity(capacity))
            {
                throw new ArgumentException(
                    $"Freed a block with capacity {capacity}, which is not a power of two. Only capacities the arena handed out may be freed.", nameof(capacity));
            }
        }

        /// <summary>
        /// Unlike the size class guards this is not <see cref="ConditionalAttribute"/>, so it survives into
        /// player builds - clamping instead would hand back a block too small for the carve that asked for it.
        /// The message is a literal for that reason too: this one gets Burst compiled, and an interpolated
        /// message would have to build a managed string on a path Burst has to accept.
        /// </summary>
        private static void ThrowBackingBudgetExceeded()
        {
            throw new InvalidOperationException(
                "Contiguous arena needs more bytes than a 32 bit handle can address (2 GB). Use ArenaAllocatorMode.Paged for this element type.");
        }
    }
}
