// <copyright project="NZCore" file="EventBus.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using NZCore.Helper;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    /// <summary>
    /// Single threaded, strongly typed. Uses an UnsafeHashMap and an UnsafeList per type.
    /// Register event types via (<see cref="Register{T}"/> 
    /// </summary>
    public unsafe struct EventBus : IDisposable
    {
        [NativeDisableUnsafePtrRestriction] private UnsafeHashMap<ulong, UnsafeList<byte>>* _map;

        private AllocatorManager.AllocatorHandle _allocator;

        /// <summary>Allocates the bus. Call <see cref="Register{T}"/> for each event type before writing.</summary>
        public static EventBus Create(int initialTypeCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            var bus = default(EventBus);
            bus._allocator = allocator;
            bus._map = AllocMap<ulong, UnsafeList<byte>>(initialTypeCapacity, allocator);

            return bus;
        }

        /// <summary>
        /// Registers a single-threaded event type backed by UnsafeList.
        /// Write via <see cref="Write{T}"/> from a single thread only.
        /// </summary>
        public void Register<T>(int capacity) where T : unmanaged
        {
            var hash = StableTypeHashHelper.GetFixedHash(typeof(T));

            if (_map->ContainsKey(hash))
            {
                return;
            }

            Hash<T>.Value.Data = hash;
            _map->Add(hash, new UnsafeList<byte>(capacity * UnsafeUtility.SizeOf<T>(), _allocator.ToAllocator, NativeArrayOptions.UninitializedMemory));
        }

        /// <summary>Disposes all allocations.</summary>
        public void Dispose()
        {
            foreach (var kvPair in *_map)
            {
                kvPair.Value.Dispose();
            }

            _map->Dispose();
            Memory.Unmanaged.Free(_map, _allocator);
            _map = null;
        }

        /// <summary>Total queued events for <typeparamref name="T"/>.</summary>
        public int Count<T>() where T : unmanaged
        {
            var hash = Hash<T>.Value.Data;
            var size = UnsafeUtility.SizeOf<T>();

            if (_map->TryGetRefValue(hash, out var list))
            {
                return list->m_length / size;
            }

            return 0;
        }

        /// <summary>Clears all events</summary>
        public void Clear()
        {
            var enumerator = _map->GetEnumerator();
            while (enumerator.MoveNext())
            {
                ref var val = ref enumerator.Current.Value;
                val.Clear();
            }
        }
        
        /// <summary>Clears all queued events for <typeparamref name="T"/>.</summary>
        public void ClearBus<T>() where T : unmanaged
        {
            var hash = Hash<T>.Value.Data;
            
            if (_map->TryGetRefValue(hash, out var item))
            {
                item->Clear();
            }
        }

        /// <summary>
        /// Single-threaded write for types registered via <see cref="Register{T}"/>.
        /// Call from the main thread or a non-parallel job only.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(in T evt) where T : unmanaged
        {
            var hash = Hash<T>.Value.Data;

            if (_map->TryGetRefValue(hash, out UnsafeList<byte>* list))
            {
                fixed (void* evtPtr = &evt)
                {
                    AppendBytes(list, evtPtr, UnsafeUtility.SizeOf<T>());
                }
            }
        }

        /// <summary>Returns a sequential reader for <typeparamref name="T"/>.</summary>
        public Reader<T> AsReader<T>() where T : unmanaged
        {
            var hash = Hash<T>.Value.Data;

            if (_map->TryGetRefValue(hash, out UnsafeList<byte>* list))
            {
                return new Reader<T>(*list);
            }

            return default;
        }

        public bool TryGetFirst<T>(out T evt)
            where T : unmanaged
        {
            var hash = Hash<T>.Value.Data;
            var size = UnsafeUtility.SizeOf<T>();

            if (_map->TryGetRefValue(hash, out UnsafeList<byte>* list))
            {
                if (list->m_length >= size)
                {
                    evt = *(T*)list->Ptr;
                    return true;
                }
            }

            evt = default;
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendBytes(UnsafeList<byte>* list, void* src, int size)
        {
            var oldLen = list->m_length;
            var newLen = oldLen + size;
            if (newLen > list->Capacity)
            {
                list->Resize(newLen);
            }
            else
            {
                list->m_length = newLen;
            }

            UnsafeUtility.MemCpy(list->Ptr + oldLen, src, size);
        }

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

        /// <summary>Per-type static hash cache. Populated by <see cref="Register{T}"/>.</summary>
        public static class Hash<T> where T : unmanaged
        {
            public static readonly SharedStatic<ulong> Value = SharedStatic<ulong>.GetOrCreate<EventBus, T>();
        }

        /// <summary>
        /// Single threaded reader for <typeparamref name="T"/> that have been registered with <see cref="Register{T}"/>.
        /// </summary>
        public struct Reader<T> where T : unmanaged
        {
            [NativeDisableUnsafePtrRestriction] private readonly UnsafeList<byte> _singleList;
            private int _byteIndex;
            private T* _current;
            public T Current => *_current;

            internal Reader(UnsafeList<byte> singleList)
            {
                _singleList = singleList;
                _byteIndex = 0;
                _current = null;
            }

            public bool MoveNext()
            {
                var size = UnsafeUtility.SizeOf<T>();
               
                if (_byteIndex + size <= _singleList.m_length)
                {
                    _current = (T*)(_singleList.Ptr + _byteIndex);
                    _byteIndex += size;
                    return true;
                }

                return false;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext(out T evt)
            {
                var size = UnsafeUtility.SizeOf<T>();
               
                if (_byteIndex + size <= _singleList.m_length)
                {
                    _current = (T*)(_singleList.Ptr + _byteIndex);
                    evt = *_current;
                    _byteIndex += size;
                    return true;
                }

                evt = default;
                return false;
            }

            /// <summary>Total buffered events for <typeparamref name="T"/>.</summary>
            public int Count()
            {
                var size = UnsafeUtility.SizeOf<T>();
                return _singleList.m_length / size;
            }
        }
    }
}