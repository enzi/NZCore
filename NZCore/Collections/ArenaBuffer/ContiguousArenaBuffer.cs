// <copyright project="NZCore" file="ContiguousArenaBuffer.cs">
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
    /// A DynamicBuffer shaped view over a block of a per element type <see cref="ContiguousArenaAllocator"/>.
    /// The same API as <see cref="ArenaBuffer{T}"/>, over an arena that is one contiguous block instead of
    /// pages. Which one an element type gets is chosen with <see cref="ArenaBufferAttribute.Mode"/>.
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
    /// Pointer lifetime is weaker than the paged buffer's, and this is the whole trade of the mode: growing
    /// <b>any</b> buffer of this element type can reallocate the arena and move it, so a <see cref="GetUnsafePtr"/>
    /// result or an <see cref="AsNativeArray"/> must not be held across a growth of any buffer of this type,
    /// not merely this one. Handles are unaffected, since they are offsets rather than addresses.
    ///
    /// There is deliberately no cached base pointer here, unlike <see cref="ArenaBuffer{T}"/>. Resolving is a
    /// single add, which is cheaper than the cache check that exists to amortize the paged double load - and
    /// caching an address in a arena that moves is exactly the bug the mode invites.
    /// </summary>
    [DebuggerDisplay("Length = {Length}, Capacity = {Capacity}, Handle = {Handle}")]
    public unsafe struct ContiguousArenaBuffer<T>
        where T : unmanaged, IArenaBuffer
    {
        [NativeDisableUnsafePtrRestriction] [NoAlias]
        private readonly ContiguousArenaAllocator* _arena;

        [NativeDisableUnsafePtrRestriction] [NoAlias]
        private readonly ArenaBufferRefData* _ref;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // The handle guarding the component this buffer writes through, obtained from the lookup or type
        // handle that produced it. The arena itself is not a NativeContainer and is not tracked.
        private AtomicSafetyHandle m_Safety;
        private readonly byte m_IsReadOnly;

        public ContiguousArenaBuffer(ContiguousArenaAllocator* arena, ArenaBufferRefData* refData, AtomicSafetyHandle safety, bool isReadOnly)
        {
            _arena = arena;
            _ref = refData;
            m_Safety = safety;
            m_IsReadOnly = (byte)(isReadOnly ? 1 : 0);
        }
#else
        public ContiguousArenaBuffer(ContiguousArenaAllocator* arena, ArenaBufferRefData* refData)
        {
            _arena = arena;
            _ref = refData;
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

            if (!_ref->IsReserved || ContiguousArenaAllocator.RoundCapacity(_ref->Length) >= _ref->Capacity)
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

        public void CopyFrom(ContiguousArenaBuffer<T> source)
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
        public ContiguousArenaBuffer<TU> Reinterpret<TU>()
            where TU : unmanaged, IArenaBuffer
        {
            CheckReinterpretSize<TU>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new ContiguousArenaBuffer<TU>(_arena, _ref, m_Safety, m_IsReadOnly == 1);
#else
            return new ContiguousArenaBuffer<TU>(_arena, _ref);
#endif
        }

        /// <summary>
        /// Resolves the block. Readonly and pure so Burst can lift it out of a loop, same as the paged
        /// buffer's - the difference is that this one is an add rather than a page table load.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly T* GetBasePtr()
        {
            var handle = _ref->Handle;

            // An unreserved record owns no block. Length is zero in that state, so callers never dereference
            // this, but resolving -1 would address the byte before the arena.
            return handle == ArenaBufferRefData.Unreserved ? null : (T*)_arena->Resolve(handle);
        }

        /// <summary>Supports foreach without going through IEnumerable, which would box in Burst compiled code.</summary>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        public struct Enumerator
        {
            private readonly ContiguousArenaBuffer<T> _buffer;
            private int _index;

            public Enumerator(ContiguousArenaBuffer<T> buffer)
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
                throw new IndexOutOfRangeException($"Index {index} is out of range in ContiguousArenaBuffer of '{_ref->Length}' Length.");
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
                    $"Cannot reinterpret ContiguousArenaBuffer<{typeof(T)}> ({sizeof(T)} bytes) as ContiguousArenaBuffer<{typeof(TU)}> ({sizeof(TU)} bytes), the element sizes differ.");
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
