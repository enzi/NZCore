// <copyright project="NZCore" file="UntypedDynamicBuffer.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace NZCore
{
    public unsafe struct UntypedDynamicBuffer
    {
        [NativeDisableUnsafePtrRestriction] [NoAlias]
        private readonly BufferHeader* _buffer;

        private readonly int _internalCapacity;
        private readonly int _elementSize;
        private readonly int _alignOf;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety0;
        internal AtomicSafetyHandle m_Safety1;
        internal int m_SafetyReadOnlyCount;
        internal int m_SafetyReadWriteCount;

        private readonly byte m_IsReadOnly;
        private readonly byte m_UseMemoryInitPattern;
        private readonly byte m_MemoryInitPattern;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal UntypedDynamicBuffer(BufferHeader* header, AtomicSafetyHandle safety, AtomicSafetyHandle arrayInvalidationSafety, bool isReadOnly,
            bool useMemoryInitPattern, byte memoryInitPattern, int internalCapacity, int elementSize, int alignOf)
        {
            _buffer = header;
            m_Safety0 = safety;
            m_Safety1 = arrayInvalidationSafety;
            m_SafetyReadOnlyCount = isReadOnly ? 2 : 0;
            m_SafetyReadWriteCount = isReadOnly ? 0 : 2;
            m_IsReadOnly = (byte)(isReadOnly ? 1 : 0);
            _internalCapacity = internalCapacity;
            m_UseMemoryInitPattern = (byte)(useMemoryInitPattern ? 1 : 0);
            m_MemoryInitPattern = memoryInitPattern;
            _alignOf = alignOf;
            _elementSize = elementSize;

            //AtomicSafetyHandle.SetNestedContainer(m_Safety0, true);
            //AtomicSafetyHandle.SetNestedContainer(m_Safety1, true);
        }

#else
        internal UntypedDynamicBuffer(BufferHeader* header, int internalCapacity, int elementSize, int alignOf)
        {
            _buffer = header;
            _internalCapacity = internalCapacity;
            _alignOf = alignOf;
            ElementSize = elementSize;
        }
#endif

        public int Length
        {
            get
            {
                CheckReadAccess();
                return _buffer->Length;
            }
            set => ResizeUninitialized(value);
        }

        public int Capacity
        {
            get
            {
                CheckReadAccess();
                return _buffer->Capacity;
            }
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (value < Length)
                {
                    throw new InvalidOperationException($"Capacity {value} can't be set smaller than Length {Length}");
                }
#endif
                CheckWriteAccessAndInvalidateArrayAliases();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                BufferHeader.SetCapacity(_buffer, value, _elementSize, _alignOf, BufferHeader.TrashMode.RetainOldData, m_UseMemoryInitPattern == 1,
                    m_MemoryInitPattern, _internalCapacity);
#else
                BufferHeader.SetCapacity(_buffer, value, ElementSize, _alignOf, BufferHeader.TrashMode.RetainOldData, false, 0, _internalCapacity);
#endif
            }
        }

        public bool IsEmpty => !IsCreated || Length == 0;
        public bool IsCreated => _buffer != null;


        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private void CheckBounds(int index)
        {
            if ((uint)index >= (uint)Length)
            {
                throw new IndexOutOfRangeException($"Index {index} is out of range in DynamicBuffer of '{Length}' Length.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckReadAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety0);
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety1);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckWriteAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety0);
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety1);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckWriteAccessAndInvalidateArrayAliases()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety0);
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety1);
#endif
        }

        public void* this[int index]
        {
            get
            {
                CheckReadAccess();
                CheckBounds(index);
                return BufferHeader.GetElementPointer(_buffer) + index * _elementSize;
            }
            set
            {
                CheckWriteAccess();
                CheckBounds(index);
                var ptr = BufferHeader.GetElementPointer(_buffer) + index * _elementSize;
                UnsafeUtility.MemCpy(ptr, value, _elementSize);
            }
        }

        public void ResizeUninitialized(int length)
        {
            EnsureCapacity(length);
            _buffer->Length = length;
        }

        public void Resize(int length, NativeArrayOptions options)
        {
            EnsureCapacity(length);

            var oldLength = _buffer->Length;
            _buffer->Length = length;
            if (options == NativeArrayOptions.ClearMemory && oldLength < length)
            {
                var num = length - oldLength;
                var ptr = BufferHeader.GetElementPointer(_buffer);
                UnsafeUtility.MemClear(ptr + oldLength * _elementSize, num * _elementSize);
            }
        }

        public void EnsureCapacity(int length)
        {
            CheckWriteAccessAndInvalidateArrayAliases();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            BufferHeader.EnsureCapacity(_buffer, length, _elementSize, _alignOf, BufferHeader.TrashMode.RetainOldData, m_UseMemoryInitPattern == 1,
                m_MemoryInitPattern);
#else
            BufferHeader.EnsureCapacity(_buffer, length, ElementSize, _alignOf, BufferHeader.TrashMode.RetainOldData, false, 0);
#endif
        }

        public void Clear()
        {
            CheckWriteAccessAndInvalidateArrayAliases();

            _buffer->Length = 0;
        }

        public void TrimExcess()
        {
            CheckWriteAccessAndInvalidateArrayAliases();

            var oldPtr = _buffer->Pointer;
            var length = _buffer->Length;

            if (length == Capacity || oldPtr == null)
            {
                return;
            }

            bool isInternal;
            byte* newPtr;

            // If the size fits in the internal buffer, prefer to move the elements back there.
            if (length <= _internalCapacity)
            {
                newPtr = (byte*)(_buffer + 1);
                isInternal = true;
            }
            else
            {
                newPtr = (byte*)Memory.Unmanaged.Allocate((long)_elementSize * length, _alignOf, Allocator.Persistent);
                isInternal = false;
            }

            UnsafeUtility.MemCpy(newPtr, oldPtr, (long)_elementSize * length);

            _buffer->Capacity = Math.Max(length, _internalCapacity);
            _buffer->Pointer = isInternal ? null : newPtr;

            Memory.Unmanaged.Free(oldPtr, Allocator.Persistent);
        }

        public int Add(void* elem)
        {
            CheckWriteAccess();
            var length = Length;
            ResizeUninitialized(length + 1);
            this[length] = elem;
            return length;
        }

        public void Insert(int index, void* elem)
        {
            CheckWriteAccess();
            var length = Length;
            ResizeUninitialized(length + 1);
            CheckBounds(index); //CheckBounds after ResizeUninitialized since index == length is allowed
            var basePtr = BufferHeader.GetElementPointer(_buffer);
            UnsafeUtility.MemMove(basePtr + (index + 1) * _elementSize, basePtr + index * _elementSize, (long)_elementSize * (length - index));
            this[index] = elem;
        }

        public void AddRange(void* elements, int count)
        {
            CheckWriteAccess();
            var oldLength = Length;
            ResizeUninitialized(oldLength + count);

            var basePtr = BufferHeader.GetElementPointer(_buffer);
            UnsafeUtility.MemCpy(basePtr + (long)oldLength * _elementSize, elements, (long)_elementSize * count);
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

            var basePtr = BufferHeader.GetElementPointer(_buffer);

            UnsafeUtility.MemMove(basePtr + index * _elementSize, basePtr + (index + count) * _elementSize, (long)_elementSize * (Length - count - index));

            _buffer->Length -= count;
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

            ref var l = ref _buffer->Length;
            var basePtr = BufferHeader.GetElementPointer(_buffer);
            var copyFrom = math.max(l - count, index + count);
            void* dst = basePtr + index * _elementSize;
            void* src = basePtr + copyFrom * _elementSize;
            UnsafeUtility.MemMove(dst, src, (l - copyFrom) * _elementSize);
            l -= count;
        }

        public void RemoveAt(int index)
        {
            RemoveRange(index, 1);
        }

        public void RemoveAtSwapBack(int index)
        {
            CheckWriteAccess();
            CheckBounds(index);

            ref var l = ref _buffer->Length;
            l -= 1;
            var newLength = l;
            if (index != newLength)
            {
                var basePtr = BufferHeader.GetElementPointer(_buffer);
                this[index] = basePtr + newLength; // todo check if this is correct
                //UnsafeUtility.WriteArrayElement(basePtr, index, UnsafeUtility.ReadArrayElement<T>(basePtr, newLength));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void* GetUnsafePtr()
        {
            CheckWriteAccess();
            return BufferHeader.GetElementPointer(_buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void* GetUnsafeReadOnlyPtr()
        {
            CheckReadAccess();
            return BufferHeader.GetElementPointer(_buffer);
        }

        public DynamicBuffer<TU> Reinterpret<TU>() where TU : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new DynamicBuffer<TU>(_buffer, m_Safety0, m_Safety1, m_IsReadOnly == 1, m_UseMemoryInitPattern == 1, m_MemoryInitPattern, _internalCapacity);
#else
            return new DynamicBuffer<TU>(_buffer, _internalCapacity);
#endif
        }

        public NativeArray<T> AsNativeArray<T>()
            where T : unmanaged
        {
            CheckReadAccess();

            var shadow = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(BufferHeader.GetElementPointer(_buffer), Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var handle = m_Safety1;
            AtomicSafetyHandle.UseSecondaryVersion(ref handle);
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref shadow, handle);

#endif
            return shadow;
        }

        public NativeArray<T> ToNativeArray<T>(AllocatorManager.AllocatorHandle allocator)
            where T : unmanaged =>
            CollectionHelper.CreateNativeArray<T>(AsNativeArray<T>(), allocator);

        public void CopyFrom<T>(DynamicBuffer<T> v)
            where T : unmanaged
        {
            ResizeUninitialized(v.Length);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(v.m_Safety0);
            AtomicSafetyHandle.CheckReadAndThrow(v.m_Safety1);
#endif
            CheckWriteAccess();

            var vHeader = v.GetBufferHeader();
            UnsafeUtility.MemCpy(BufferHeader.GetElementPointer(_buffer), vHeader->Pointer, Length * UnsafeUtility.SizeOf<T>());
        }
    }
}