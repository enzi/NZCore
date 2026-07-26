// <copyright project="NZCore" file="SharedChunkPagedArenaBuffer.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NZCore
{
    /// <summary>
    /// A DynamicBuffer shaped view over a block of the process wide <see cref="SharedArenaAllocator"/>. The
    /// same API as <see cref="ArenaBuffer{T}"/>, over an arena that every element type registered as
    /// <see cref="ArenaAllocatorMode.SharedChunkPaged"/> shares.
    ///
    /// This is <see cref="ChunkPagedArenaBuffer{T}"/> taken one step further. There a chunk got a page per
    /// element type; here a chunk gets one page holding <b>all</b> of its arena buffer types, laid out as one
    /// contiguous run per type. An entity carrying several arena buffers therefore has all of them within a
    /// page of each other, and a system reading two of those types over a chunk touches one page rather than
    /// two. Resolving is still a compare against the page the accessor already resolved, then an add.
    ///
    /// The arena being shared is why it is byte oriented: the types in it have different element sizes, so
    /// every call that sizes a block passes this buffer's own <c>sizeof(T)</c>.
    ///
    /// The page remains a hint. An entity that moved in, or a buffer that grew past the class its chunk page
    /// was laid out at, holds a handle into another page and takes the fallback resolve instead of reading the
    /// wrong memory. Pages never move, so pointer lifetime matches <see cref="ArenaBuffer{T}"/>.
    /// </summary>
    [DebuggerDisplay("Length = {Length}, Capacity = {Capacity}, Handle = {Handle}")]
    public unsafe struct SharedChunkPagedArenaBuffer<T>
        where T : unmanaged, IArenaBuffer
    {
        [NativeDisableUnsafePtrRestriction] [NoAlias]
        private readonly SharedArenaAllocator* _arena;

        [NativeDisableUnsafePtrRestriction] [NoAlias]
        private readonly ArenaBufferRefData* _ref;

        // The page the chunk accessor resolved once for this chunk, and its index so a handle can be tested
        // against it. Both are readonly, which keeps GetBasePtr pure and lets Burst hoist it out of a loop.
        [NativeDisableUnsafePtrRestriction] [NoAlias]
        private readonly byte* _pageBase;

        private readonly int _pageIndex;

        /// <summary>A page index no handle can carry, used when there is no chunk page to inherit.</summary>
        public const int NoPage = -1;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // The handle guarding the component this buffer writes through, obtained from the lookup or type
        // handle that produced it. The arena itself is not a NativeContainer and is not tracked.
        private AtomicSafetyHandle m_Safety;
        private readonly byte m_IsReadOnly;

        public SharedChunkPagedArenaBuffer(SharedArenaAllocator* arena, ArenaBufferRefData* refData, int pageIndex, byte* pageBase, AtomicSafetyHandle safety, bool isReadOnly)
        {
            _arena = arena;
            _ref = refData;
            _pageIndex = pageIndex;
            _pageBase = pageBase;
            m_Safety = safety;
            m_IsReadOnly = (byte)(isReadOnly ? 1 : 0);
        }

        public SharedChunkPagedArenaBuffer(SharedArenaAllocator* arena, ArenaBufferRefData* refData, AtomicSafetyHandle safety, bool isReadOnly)
            : this(arena, refData, NoPage, null, safety, isReadOnly)
        {
        }
#else
        public SharedChunkPagedArenaBuffer(SharedArenaAllocator* arena, ArenaBufferRefData* refData, int pageIndex, byte* pageBase)
        {
            _arena = arena;
            _ref = refData;
            _pageIndex = pageIndex;
            _pageBase = pageBase;
        }

        public SharedChunkPagedArenaBuffer(SharedArenaAllocator* arena, ArenaBufferRefData* refData)
            : this(arena, refData, NoPage, null)
        {
        }
#endif

        /// <summary>Number of elements in the buffer. Setting it resizes without initializing new elements.</summary>
        public int Length
        {
            get
            {
                CheckReadAccess();
                return _ref->Length;
            }
            set => ResizeUninitialized(value);
        }

        /// <summary>
        /// Number of elements the block can hold. Always a power of two - setting it rounds up.
        /// Setting a value smaller than <see cref="Length"/> throws.
        /// </summary>
        public int Capacity
        {
            get
            {
                CheckReadAccess();
                return _ref->Capacity;
            }
            set
            {
                CheckCapacityShrink(value);
                CheckWriteAccess();
                _arena->Reallocate(ref *_ref, value, sizeof(T));
            }
        }

        /// <summary>Byte offset of this buffer's block inside the arena. Diagnostics only.</summary>
        public int Handle
        {
            get
            {
                CheckReadAccess();
                return _ref->Handle;
            }
        }

        public bool IsEmpty => !IsCreated || Length == 0;

        public bool IsCreated => _arena != null && _ref != null;

        public T this[int index]
        {
            readonly get
            {
                CheckReadAccess();
                CheckBounds(index);
                return UnsafeUtility.ReadArrayElement<T>(GetBasePtr(), index);
            }
            set
            {
                CheckWriteAccess();
                CheckBounds(index);
                UnsafeUtility.WriteArrayElement(GetBasePtr(), index, value);
            }
        }

        /// <summary>Reference to an element, so callers can mutate fields in place without a copy back.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ElementAt(int index)
        {
            CheckWriteAccess();
            CheckBounds(index);
            return ref UnsafeUtility.ArrayElementAsRef<T>(GetBasePtr(), index);
        }

        /// <summary>Grows the block if needed so it can hold <paramref name="length"/> elements.</summary>
        public void EnsureCapacity(int length)
        {
            CheckWriteAccess();

            if (_ref->IsReserved && length <= _ref->Capacity)
            {
                return;
            }

            // On first touch of a record that was requested but not yet reserved, honour the capacity that
            // was asked for instead of shrinking to whatever this single call happens to need.
            var required = _ref->IsReserved ? length : math.max(length, _ref->Capacity);
            _arena->Reallocate(ref *_ref, required, sizeof(T));
        }

        /// <summary>Sets the length, leaving any new elements uninitialized.</summary>
        public void ResizeUninitialized(int length)
        {
            CheckNegativeLength(length);
            EnsureCapacity(length);
            _ref->Length = length;
        }

        public void Resize(int length, NativeArrayOptions options)
        {
            CheckNegativeLength(length);
            EnsureCapacity(length);

            var oldLength = _ref->Length;
            _ref->Length = length;

            if (options == NativeArrayOptions.ClearMemory && oldLength < length)
            {
                UnsafeUtility.MemClear(GetBasePtr() + oldLength, (long)(length - oldLength) * sizeof(T));
            }
        }

        public void Clear()
        {
            CheckWriteAccess();
            _ref->Length = 0;
        }

        /// <summary>
        /// Shrinks the block to the smallest size class that still holds <see cref="Length"/> elements,
        /// returning the difference to the arena's free lists.
        /// </summary>
        public void TrimExcess()
        {
            CheckWriteAccess();

            if (!_ref->IsReserved || ArenaAllocator.RoundCapacity(_ref->Length) >= _ref->Capacity)
            {
                return;
            }

            _arena->Reallocate(ref *_ref, _ref->Length, sizeof(T));
        }

        /// <summary>Appends an element and returns the index it was written to.</summary>
        public int Add(in T elem)
        {
            CheckWriteAccess();
            var length = Length;
            ResizeUninitialized(length + 1);
            UnsafeUtility.WriteArrayElement(GetBasePtr(), length, elem);
            return length;
        }

        public void Insert(int index, in T elem)
        {
            CheckWriteAccess();
            var length = Length;
            ResizeUninitialized(length + 1);
            CheckBounds(index); // after the resize, since index == old length is valid

            var basePtr = GetBasePtr();
            UnsafeUtility.MemMove(basePtr + index + 1, basePtr + index, (long)(length - index) * sizeof(T));
            UnsafeUtility.WriteArrayElement(basePtr, index, elem);
        }

        /// <summary>
        /// Appends <paramref name="count"/> elements. <paramref name="elements"/> must not point into this
        /// arena, since growing can move the whole block and would leave it dangling.
        /// </summary>
        public void AddRange(T* elements, int count)
        {
            CheckWriteAccess();
            var oldLength = Length;
            ResizeUninitialized(oldLength + count);

            UnsafeUtility.MemCpy(GetBasePtr() + oldLength, elements, (long)count * sizeof(T));
        }

        public void AddRange(NativeArray<T> elements)
        {
            AddRange((T*)elements.GetUnsafeReadOnlyPtr(), elements.Length);
        }

        public void RemoveRange(int index, int count)
        {
            CheckWriteAccess();
            CheckBounds(index);

            if (count == 0)
            {
                return;
            }

            CheckBounds(index + count - 1);

            var basePtr = GetBasePtr();
            UnsafeUtility.MemMove(basePtr + index, basePtr + index + count, (long)(Length - count - index) * sizeof(T));

            _ref->Length -= count;
        }

        public void RemoveRangeSwapBack(int index, int count)
        {
            CheckWriteAccess();
            CheckBounds(index);

            if (count == 0)
            {
                return;
            }

            CheckBounds(index + count - 1);

            ref var length = ref _ref->Length;
            var basePtr = GetBasePtr();
            var copyFrom = math.max(length - count, index + count);

            UnsafeUtility.MemMove(basePtr + index, basePtr + copyFrom, (long)(length - copyFrom) * sizeof(T));

            length -= count;
        }

        public void RemoveAt(int index)
        {
            RemoveRange(index, 1);
        }

        public void RemoveAtSwapBack(int index)
        {
            CheckWriteAccess();
            CheckBounds(index);

            ref var length = ref _ref->Length;
            length -= 1;

            var newLength = length;
            if (index != newLength)
            {
                var basePtr = GetBasePtr();
                UnsafeUtility.WriteArrayElement(basePtr, index, UnsafeUtility.ReadArrayElement<T>(basePtr, newLength));
            }
        }

        /// <summary>
        /// Pointer to the first element. Invalidated whenever <b>any</b> buffer of this element type grows
        /// past the arena's tail, not only this one - do not cache it across an <see cref="Add"/>,
        /// <see cref="Resize"/> or <see cref="EnsureCapacity"/> anywhere.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetUnsafePtr()
        {
            CheckWriteAccess();
            return GetBasePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetUnsafeReadOnlyPtr()
        {
            CheckReadAccess();
            return GetBasePtr();
        }

        /// <summary>
        /// A NativeArray aliasing the block. Invalidated whenever any buffer of this element type grows past
        /// the arena's tail.
        /// </summary>
        public NativeArray<T> AsNativeArray()
        {
            CheckReadAccess();

            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(GetBasePtr(), Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_Safety);
#endif
            return array;
        }

        public NativeArray<T> ToNativeArray(AllocatorManager.AllocatorHandle allocator)
        {
            return CollectionHelper.CreateNativeArray(AsNativeArray(), allocator);
        }

        public void CopyFrom(T* source, int count)
        {
            ResizeUninitialized(count);
            CheckWriteAccess();
            UnsafeUtility.MemCpy(GetBasePtr(), source, (long)count * sizeof(T));
        }

        public void CopyFrom(NativeArray<T> source)
        {
            CopyFrom((T*)source.GetUnsafeReadOnlyPtr(), source.Length);
        }

        public void CopyFrom(SharedChunkPagedArenaBuffer<T> source)
        {
            // Both pointers are fetched after the resize: growing this buffer can move the whole arena, which
            // would leave a source pointer taken beforehand dangling.
            var count = source.Length;
            ResizeUninitialized(count);
            CheckWriteAccess();
            UnsafeUtility.MemCpy(GetBasePtr(), source.GetUnsafeReadOnlyPtr(), (long)count * sizeof(T));
        }

        /// <summary>
        /// Views the same block as a different element type of identical size. The returned buffer keeps
        /// this buffer's arena, so growing it still allocates from <typeparamref name="T"/>'s arena.
        /// </summary>
        public SharedChunkPagedArenaBuffer<TU> Reinterpret<TU>()
            where TU : unmanaged, IArenaBuffer
        {
            CheckReinterpretSize<TU>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new SharedChunkPagedArenaBuffer<TU>(_arena, _ref, _pageIndex, _pageBase, m_Safety, m_IsReadOnly == 1);
#else
            return new SharedChunkPagedArenaBuffer<TU>(_arena, _ref, _pageIndex, _pageBase);
#endif
        }

        /// <summary>
        /// Resolves the block, off the chunk's page when the handle belongs to it. Readonly and pure so Burst
        /// can lift it out of a loop, same as the other buffers.
        ///
        /// The page test is what makes the mode safe: a handle from another page - a moved entity, or a buffer
        /// that grew out of its chunk's class - simply takes the slower path rather than reading the wrong
        /// memory. In a chunk that has not been reshuffled the branch is perfectly predicted and no page table
        /// load happens at all.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly T* GetBasePtr()
        {
            var handle = _ref->Handle;

            // An unreserved record owns no block. Length is zero in that state, so callers never dereference
            // this, but resolving -1 would index the page table out of bounds.
            if (handle == ArenaBufferRefData.Unreserved)
            {
                return null;
            }

            if (Hint.Likely(handle >> SharedArenaAllocator.PageShift == _pageIndex))
            {
                return (T*)(_pageBase + (handle & PageOffsetMask));
            }

            return (T*)_arena->Resolve(handle);
        }

        private const int PageOffsetMask = SharedArenaAllocator.MaxPageBytes - 1;

        /// <summary>Supports foreach without going through IEnumerable, which would box in Burst compiled code.</summary>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        public struct Enumerator
        {
            private readonly SharedChunkPagedArenaBuffer<T> _buffer;
            private int _index;

            public Enumerator(SharedChunkPagedArenaBuffer<T> buffer)
            {
                _buffer = buffer;
                _index = -1;
            }

            public bool MoveNext()
            {
                _index++;
                return _index < _buffer.Length;
            }

            public void Reset()
            {
                _index = -1;
            }

            public T Current => _buffer[_index];
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private readonly void CheckBounds(int index)
        {
            if ((uint)index >= (uint)_ref->Length)
            {
                throw new IndexOutOfRangeException($"Index {index} is out of range in SharedChunkPagedArenaBuffer of '{_ref->Length}' Length.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckNegativeLength(int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), $"Length {length} can't be negative.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private readonly void CheckCapacityShrink(int capacity)
        {
            if (capacity < _ref->Length)
            {
                throw new InvalidOperationException($"Capacity {capacity} can't be set smaller than Length {_ref->Length}");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckReinterpretSize<TU>()
            where TU : unmanaged
        {
            if (sizeof(TU) != sizeof(T))
            {
                throw new InvalidOperationException(
                    $"Cannot reinterpret SharedChunkPagedArenaBuffer<{typeof(T)}> ({sizeof(T)} bytes) as SharedChunkPagedArenaBuffer<{typeof(TU)}> ({sizeof(TU)} bytes), the element sizes differ.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private readonly void CheckReadAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private readonly void CheckWriteAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }
    }
}
