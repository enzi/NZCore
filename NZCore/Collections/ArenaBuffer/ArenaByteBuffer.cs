// <copyright project="NZCore" file="ArenaByteBuffer.cs">
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
    /// The untyped form of <see cref="ArenaBuffer{T}"/>: the same view over an arena block, but carrying its
    /// element size as a field instead of taking it from <c>sizeof(T)</c>.
    ///
    /// This exists for the two cases the generic buffer cannot serve. The first is a raw byte block whose
    /// layout is decided by data rather than by a struct - stat buffers size themselves from the authoring
    /// schema at runtime, so there is no element type to name. The second is generic code parameterised on a
    /// <c>*Ref</c> component alone: the ref type identifies the record and the arena, but not the element
    /// type, so <see cref="ArenaBuffer{T}"/> is out of reach.
    ///
    /// <see cref="Length"/> and <see cref="Capacity"/> count elements, matching
    /// <see cref="ArenaBufferRefData"/> and the arena's own allocation units. For a genuine byte buffer the
    /// element size is one and the two readings coincide.
    ///
    /// Threading and pointer lifetime are exactly as <see cref="ArenaBuffer{T}"/> describes.
    /// </summary>
    [DebuggerDisplay("Length = {Length}, Capacity = {Capacity}, ElementSize = {ElementSize}")]
    public unsafe struct ArenaByteBuffer
    {
        [NativeDisableUnsafePtrRestriction] [NoAlias]
        private readonly ArenaAllocator* _arena;

        [NativeDisableUnsafePtrRestriction] [NoAlias]
        private readonly ArenaBufferRefData* _ref;

        // Passed in rather than read from _arena->ElementSize on use. The allocator knows its own element
        // size, but reaching it is a dependent load on a second cache line, and every byte count below needs
        // it - so it is fetched once at construction and kept beside the pointers the caller is already
        // touching. Taking it as a parameter also lets a caller view a block at a different granularity.
        private readonly int _elementSize;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private readonly byte m_IsReadOnly;

        public ArenaByteBuffer(ArenaAllocator* arena, ArenaBufferRefData* refData, int elementSize, AtomicSafetyHandle safety, bool isReadOnly)
        {
            _arena = arena;
            _ref = refData;
            _elementSize = elementSize;
            m_Safety = safety;
            m_IsReadOnly = (byte)(isReadOnly ? 1 : 0);
        }
#else
        public ArenaByteBuffer(ArenaAllocator* arena, ArenaBufferRefData* refData, int elementSize)
        {
            _arena = arena;
            _ref = refData;
            _elementSize = elementSize;
        }
#endif

        /// <summary>Bytes per element, as the type was registered.</summary>
        public readonly int ElementSize => _elementSize;

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

        /// <summary>Size of the live region in bytes, for callers doing their own copies.</summary>
        public int ByteLength
        {
            get
            {
                CheckReadAccess();
                return _ref->Length * _elementSize;
            }
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

        public bool IsEmpty => !IsCreated || Length == 0;

        public bool IsCreated => _arena != null && _ref != null;

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
                UnsafeUtility.MemClear(GetBasePtr() + (long)oldLength * _elementSize, (long)(length - oldLength) * _elementSize);
            }
        }

        public void Clear()
        {
            CheckWriteAccess();
            _ref->Length = 0;
        }

        /// <summary>
        /// Pointer to the first element. Invalidated when this buffer grows past its capacity - do not cache
        /// it across a <see cref="Resize"/> or <see cref="EnsureCapacity"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetUnsafePtr()
        {
            CheckWriteAccess();
            return GetBasePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetUnsafeReadOnlyPtr()
        {
            CheckReadAccess();
            return GetBasePtr();
        }

        /// <summary>Replaces the contents with <paramref name="count"/> elements from <paramref name="source"/>.</summary>
        public void CopyFrom(byte* source, int count)
        {
            ResizeUninitialized(count);
            CheckWriteAccess();
            UnsafeUtility.MemCpy(GetBasePtr(), source, (long)count * _elementSize);
        }

        public void CopyFrom(in ArenaByteBuffer source)
        {
            CheckElementSizeMatch(source._elementSize);

            // Both pointers are fetched after the resize: growing this buffer moves it to another block,
            // which would leave a source pointer taken beforehand dangling.
            var count = source.Length;
            ResizeUninitialized(count);
            CheckWriteAccess();
            UnsafeUtility.MemCpy(GetBasePtr(), source.GetUnsafeReadOnlyPtr(), (long)count * _elementSize);
        }

        /// <summary>
        /// Views the same block as a typed buffer. The element sizes must agree - this is the way back to
        /// <see cref="ArenaBuffer{T}"/> once a caller knows the concrete element type again.
        /// </summary>
        public ArenaBuffer<T> Reinterpret<T>()
            where T : unmanaged, IArenaBuffer
        {
            CheckReinterpretSize<T>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new ArenaBuffer<T>(_arena, _ref, m_Safety, m_IsReadOnly == 1);
#else
            return new ArenaBuffer<T>(_arena, _ref);
#endif
        }

        /// <inheritdoc cref="ArenaBuffer{T}.GetUnsafePtr"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly byte* GetBasePtr()
        {
            return (byte*)_ref->Block;
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
        private readonly void CheckElementSizeMatch(int sourceElementSize)
        {
            if (sourceElementSize != _elementSize)
            {
                throw new InvalidOperationException(
                    $"Cannot copy from an ArenaByteBuffer of element size {sourceElementSize} into one of element size {_elementSize}.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private readonly void CheckReinterpretSize<T>()
            where T : unmanaged
        {
            if (sizeof(T) != _elementSize)
            {
                throw new InvalidOperationException(
                    $"Cannot reinterpret an ArenaByteBuffer of element size {_elementSize} as ArenaBuffer<{typeof(T)}> ({sizeof(T)} bytes), the element sizes differ.");
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
