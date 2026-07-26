// <copyright project="NZCore" file="ArenaBuffer.cs">
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
    /// A DynamicBuffer shaped view over a block of a per element type <see cref="ArenaAllocator"/>.
    ///
    /// The buffer owns no memory itself. It points at the entity's <see cref="ArenaBufferRefData"/> record
    /// living in chunk memory, and at the arena that record indexes into. Every mutation that changes the
    /// handle, length or capacity is written straight back into chunk memory, so the record never goes stale.
    ///
    /// Threading: reads and in place writes within the current capacity are safe from parallel jobs, because
    /// each entity owns a disjoint block. Anything that can grow the block - <see cref="Add"/>,
    /// <see cref="Insert"/>, <see cref="Resize"/>, <see cref="EnsureCapacity"/> - allocates from the shared
    /// arena and must be single writer.
    ///
    /// Pointer lifetime: the arena's pages never move, so another buffer growing can never invalidate this
    /// one's memory. <see cref="GetUnsafePtr"/> and <see cref="AsNativeArray"/> are only invalidated by a
    /// call that grows <b>this</b> buffer past its capacity, which copies it into a larger block.
    /// </summary>
    [DebuggerDisplay("Length = {Length}, Capacity = {Capacity}, Handle = {Handle}")]
    public unsafe struct ArenaBuffer<T>
        where T : unmanaged, IArenaBuffer
    {
        [NativeDisableUnsafePtrRestriction] [NoAlias]
        private readonly ArenaAllocator* _arena;

        [NativeDisableUnsafePtrRestriction] [NoAlias]
        private readonly ArenaBufferRefData* _ref;

        // Cache for the append path only. Resolving a handle costs two dependent loads (the page table, then
        // the page); in an Add loop those cannot be hoisted, because writing Length each iteration may alias
        // the page table, so caching turns them into one hot load plus a predictable branch.
        //
        // Read paths deliberately do NOT use this. GetBasePtr is readonly and pure, which lets Burst hoist
        // the resolve out of a read loop entirely - measured 2.3x to 4.1x faster than a cached-but-mutating
        // accessor, because a mutating accessor blocks that hoist.
        //
        // Either way the record's handle is the source of truth: a moved block shows up as a mismatch, so a
        // stale cache cannot survive a reallocation.
        [NativeDisableUnsafePtrRestriction] [NoAlias]
        private T* _cachedBase;

        private int _cachedHandle;

        /// <summary>A handle value no record can hold, so the first access always resolves.</summary>
        private const int NeverResolved = int.MinValue;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // The handle guarding the component this buffer writes through, obtained from the lookup or type
        // handle that produced it. The arena itself is not a NativeContainer and is not tracked.
        private AtomicSafetyHandle m_Safety;
        private readonly byte m_IsReadOnly;

        public ArenaBuffer(ArenaAllocator* arena, ArenaBufferRefData* refData, AtomicSafetyHandle safety, bool isReadOnly)
        {
            _arena = arena;
            _ref = refData;
            _cachedBase = null;
            _cachedHandle = NeverResolved;
            m_Safety = safety;
            m_IsReadOnly = (byte)(isReadOnly ? 1 : 0);
        }
#else
        public ArenaBuffer(ArenaAllocator* arena, ArenaBufferRefData* refData)
        {
            _arena = arena;
            _ref = refData;
            _cachedBase = null;
            _cachedHandle = NeverResolved;
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
                _arena->Reallocate(ref *_ref, value);
            }
        }

        /// <summary>Packed handle of this buffer's block inside the arena. Diagnostics only.</summary>
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

            // Do not be tempted to drop the IsReserved test to save a load: it reads Handle *before* the
            // Length store below, and GetAppendBasePtr's cache check then reuses that same register. Removing
            // it forces Handle to be reloaded after the store, which measured 14-18% slower on append loops.
            if (_ref->IsReserved && length <= _ref->Capacity)
            {
                return;
            }

            // On first touch of a record that was requested but not yet reserved, honour the capacity that
            // was asked for instead of shrinking to whatever this single call happens to need.
            var required = _ref->IsReserved ? length : math.max(length, _ref->Capacity);
            _arena->Reallocate(ref *_ref, required);
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

            _arena->Reallocate(ref *_ref, _ref->Length);
        }

        /// <summary>Appends an element and returns the index it was written to.</summary>
        public int Add(in T elem)
        {
            CheckWriteAccess();
            var length = Length;
            ResizeUninitialized(length + 1);
            UnsafeUtility.WriteArrayElement(GetAppendBasePtr(), length, elem);
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
        /// buffer's own block, since growing moves the block and would leave it dangling.
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
        /// Pointer to the first element. Invalidated when this buffer grows past its capacity - do not cache
        /// it across an <see cref="Add"/>, <see cref="Resize"/> or <see cref="EnsureCapacity"/>.
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

        /// <summary>A NativeArray aliasing the block. Invalidated when this buffer grows past its capacity.</summary>
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

        public void CopyFrom(ArenaBuffer<T> source)
        {
            // Both pointers are fetched after the resize: growing this buffer moves it to another block,
            // which would leave a source pointer taken beforehand dangling.
            var count = source.Length;
            ResizeUninitialized(count);
            CheckWriteAccess();
            UnsafeUtility.MemCpy(GetBasePtr(), source.GetUnsafeReadOnlyPtr(), (long)count * sizeof(T));
        }

        /// <summary>
        /// Views the same block as a different element type of identical size. The returned buffer keeps
        /// this buffer's arena, so growing it still allocates from <typeparamref name="T"/>'s arena.
        /// </summary>
        public ArenaBuffer<TU> Reinterpret<TU>()
            where TU : unmanaged, IArenaBuffer
        {
            CheckReinterpretSize<TU>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new ArenaBuffer<TU>(_arena, _ref, m_Safety, m_IsReadOnly == 1);
#else
            return new ArenaBuffer<TU>(_arena, _ref);
#endif
        }

        /// <summary>
        /// Pure resolve, used by everything except <see cref="Add"/>. Being readonly is the point: it lets
        /// Burst lift the resolve out of a read loop.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly T* GetBasePtr()
        {
            var handle = _ref->Handle;

            // An unreserved record has no page. Length is zero in that state, so callers never dereference
            // this, but resolving -1 would index the page table out of bounds.
            return handle == ArenaBufferRefData.Unreserved ? null : (T*)_arena->Resolve(handle);
        }

        /// <summary>Cached resolve for the append path, where the pure form cannot be hoisted.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T* GetAppendBasePtr()
        {
            var handle = _ref->Handle;

            if (Hint.Unlikely(_cachedHandle != handle))
            {
                _cachedHandle = handle;
                _cachedBase = handle == ArenaBufferRefData.Unreserved ? null : (T*)_arena->Resolve(handle);
            }

            return _cachedBase;
        }

        /// <summary>Supports foreach without going through IEnumerable, which would box in Burst compiled code.</summary>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        public struct Enumerator
        {
            private readonly ArenaBuffer<T> _buffer;
            private int _index;

            public Enumerator(ArenaBuffer<T> buffer)
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
                throw new IndexOutOfRangeException($"Index {index} is out of range in ArenaBuffer of '{_ref->Length}' Length.");
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
                    $"Cannot reinterpret ArenaBuffer<{typeof(T)}> ({sizeof(T)} bytes) as ArenaBuffer<{typeof(TU)}> ({sizeof(TU)} bytes), the element sizes differ.");
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
