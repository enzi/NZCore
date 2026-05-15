// <copyright project="NZCore" file="ParallelEventBus.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using NZCore.Helper;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;

namespace NZCore
{
    /// <summary>
    /// Multi threaded, strongly typed. Uses an UnsafeHashMap and an UnsafeParallelList per type.
    /// Register event types via (<see cref="Register{T}"/> 
    /// </summary>
    public unsafe struct ParallelEventBus : IComponentData
    {
        [NativeDisableUnsafePtrRestriction] private UnsafeHashMap<ulong, UnsafeParallelList<byte>>* _parallelMap;

        private AllocatorManager.AllocatorHandle _allocator;

        /// <summary>Allocates the bus. Call <see cref="Register{T}"/> for each event type before scheduling jobs.</summary>
        public static ParallelEventBus Create(int initialTypeCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            var bus = default(ParallelEventBus);
            bus._allocator = allocator;
            bus._parallelMap = AllocMap<ulong, UnsafeParallelList<byte>>(initialTypeCapacity, allocator);

            return bus;
        }

        /// <summary>
        /// Registers a multi-threaded event type backed by UnsafeParallelList.
        /// Write safely from parallel jobs via <see cref="AsParallelWriter"/>.
        /// </summary>
        public void Register<T>(int capacityPerThread) where T : unmanaged
        {
            var hash = StableTypeHashHelper.GetFixedHash(typeof(T));
            Hash<T>.Value.Data = hash;

            if (_parallelMap->ContainsKey(hash))
            {
                return;
            }

            var tempAlloc = _allocator;
            var ptr = UnsafeParallelList<byte>.Create(capacityPerThread * UnsafeUtility.SizeOf<T>(), ref tempAlloc);
            _parallelMap->Add(hash, *ptr);
            UnsafeUtility.Free(ptr, _allocator.ToAllocator);
        }

        /// <summary>Disposes all allocations. Main thread only</summary>
        public void Dispose()
        {
            foreach (var kvPair in *_parallelMap)
            {
                kvPair.Value.Dispose();
            }

            _parallelMap->Dispose();
            Memory.Unmanaged.Free(_parallelMap, _allocator);

            _parallelMap = null;
        }

        public int Count<T>() where T : unmanaged
        {
            var hash = Hash<T>.Value.Data;
            var size = UnsafeUtility.SizeOf<T>();

            var idx = _parallelMap->m_Data.Find(hash);
            if (idx != -1)
            {
                return ParallelListPtr(_parallelMap, idx)->Count() / size;
            }

            return 0;
        }

        public void ClearBus<T>() where T : unmanaged
        {
            var hash = Hash<T>.Value.Data;

            var idx = _parallelMap->m_Data.Find(hash);
            if (idx != -1)
            {
                ParallelListPtr(_parallelMap, idx)->Clear();
            }
        }

        // <summary>Returns a parallel writer backed by this bus.</summary>
        public ParallelWriter<T> AsParallelWriter<T>() where T : unmanaged
        {
            var hash = Hash<T>.Value.Data;

            var idx = _parallelMap->m_Data.Find(hash);
            if (idx != -1)
            {
                return new ParallelWriter<T>(ParallelListPtr(_parallelMap,
                    idx));
            }

            return default;
        }

        public ParallelReader<T> AsParallelReader<T>()
            where T : unmanaged
        {
            var hash = Hash<T>.Value.Data;

            var idx = _parallelMap->m_Data.Find(hash);
            if (idx != -1)
            {
                return new ParallelReader<T>(ParallelListPtr(_parallelMap, idx));
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static UnsafeParallelList<byte>* ParallelListPtr(UnsafeHashMap<ulong, UnsafeParallelList<byte>>* mapPtr, int idx)
            => (UnsafeParallelList<byte>*)(mapPtr->m_Data.Ptr + UnsafeUtility.SizeOf<UnsafeParallelList<byte>>() * idx);

        private static UnsafeHashMap<TKey, TValue>* AllocMap<TKey, TValue>(int capacity, AllocatorManager.AllocatorHandle allocator)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var ptr = (UnsafeHashMap<TKey, TValue>*)
                Memory.Unmanaged.Allocate(
                    UnsafeUtility.SizeOf<UnsafeHashMap<TKey, TValue>>(),
                    UnsafeUtility.AlignOf<UnsafeHashMap<TKey, TValue>>(),
                    allocator.ToAllocator);
            *ptr = new UnsafeHashMap<TKey, TValue>(capacity, allocator.ToAllocator);
            return ptr;
        }

        /// <summary>Per-type static hash cache. Populated by <see cref="Register{T}"/>; read in Burst code.</summary>
        public static class Hash<T> where T : unmanaged
        {
            public static readonly SharedStatic<ulong> Value = SharedStatic<ulong>.GetOrCreate<ParallelEventBus, T>();
        }

        /// Multi threaded reader for <typeparamref name="T"/> that have been registered with <see cref="Register{T}"/>.
        public struct ParallelReader<T> where T : unmanaged
        {
            [NativeDisableUnsafePtrRestriction] private readonly UnsafeParallelList<byte>* _parallelList;
            [NativeDisableUnsafePtrRestriction] private UnsafeList<byte>* _currentThreadList;
            private int _currentIndex;
            private int _byteIndex;

            internal ParallelReader(UnsafeParallelList<byte>* parallelList)
            {
                _parallelList = parallelList;
                _currentIndex = 0;
                _byteIndex = 0;
                _currentThreadList = null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext(out T evt)
            {
                var size = UnsafeUtility.SizeOf<T>();

                if (_currentThreadList != null && _byteIndex + size <= _currentThreadList->m_length)
                {
                    evt = *(T*)(_currentThreadList->Ptr + _byteIndex);
                    _byteIndex += size;
                    return true;
                }

                while (_currentIndex < JobsUtility.ThreadIndexCount)
                {
                    _currentThreadList = _parallelList->GetUnsafeListPtr(_currentIndex);

                    _currentIndex++;
                    _byteIndex = 0;

                    if (_currentThreadList->Length > 0)
                    {
                        evt = *(T*)(_currentThreadList->Ptr + _byteIndex);
                        _byteIndex += size;
                        return true;
                    }
                }

                evt = default;
                return false;
            }

            /// <summary>Total buffered events for <typeparamref name="T"/>.</summary>
            public int Count()
            {
                var size = UnsafeUtility.SizeOf<T>();
                return _parallelList->Count() / size;
            }
        }

        public struct ParallelWriter<T> where T : unmanaged
        {
            [NativeDisableUnsafePtrRestriction] internal UnsafeParallelList<byte>* ListData;

            internal ParallelWriter(UnsafeParallelList<byte>* listData)
            {
                ListData = listData;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write(T value)
            {
                ListData->GetUnsafeListPtr(JobsUtility.ThreadIndex)
                    ->AddRangeNoResize(UnsafeUtility.AddressOf(ref value),
                    UnsafeUtility.SizeOf<T>());
            }
        }
    }
}