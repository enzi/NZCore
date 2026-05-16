// <copyright project="NZCore" file="ParallelEventBus.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.Helper;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    /// <summary>
    /// Multi threaded, strongly typed. Uses an UnsafeHashMap and an UnsafeParallelList per type.
    /// Register event types via (<see cref="Register{T}"/> 
    /// </summary>
    public unsafe struct ParallelEventBus
    {
        [NativeDisableUnsafePtrRestriction] private UnsafeHashMap<ulong, VoidPtr>* _parallelMap;

        private AllocatorManager.AllocatorHandle _allocator;

        /// <summary>Allocates the bus. Call <see cref="Register{T}"/> for each event type before scheduling jobs.</summary>
        public static ParallelEventBus Create(int initialTypeCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            var bus = default(ParallelEventBus);
            bus._allocator = allocator;
            bus._parallelMap = UnsafeCreateHelper.CreateHashMap<ulong, VoidPtr>(initialTypeCapacity, allocator);

            return bus;
        }

        /// <summary>
        /// Registers a multi-threaded event type backed by UnsafeParallelList.
        /// Write safely from parallel jobs via <see cref="AsParallelWriter"/>.
        /// </summary>
        public void Register<T>(int initialCapacity) where T : unmanaged
        {
            var hash = StableTypeHashHelper.GetFixedHash(typeof(T));
            Hash<T>.Value.Data = hash;

            if (_parallelMap->ContainsKey(hash))
            {
                return;
            }

            var listPtr = UnsafeParallelList<T>.Create(initialCapacity, ref _allocator);
            _parallelMap->Add(hash, new VoidPtr(listPtr));
        }

        /// <summary>Disposes all allocations. Main thread only</summary>
        public void Dispose()
        {
            foreach (var kvPair in *_parallelMap)
            {
                var listPtr = (UnsafeParallelList<byte>*) kvPair.Value.Ptr;
                UnsafeParallelList<byte>.Destroy(listPtr);
            }

            _parallelMap->Dispose();
            Memory.Unmanaged.Free(_parallelMap, _allocator);

            _parallelMap = null;
        }

        public int Count<T>() where T : unmanaged
        {
            var hash = Hash<T>.Value.Data;

            if (_parallelMap->TryGetRefValue(hash, out var ptr))
            {
                return ((UnsafeParallelList<T>*)ptr->Ptr)->Count();
            }

            return 0;
        }

        public void ClearBus<T>() where T : unmanaged
        {
            var hash = Hash<T>.Value.Data;
            
            if (_parallelMap->TryGetRefValue(hash, out var ptr))
            {
                ((UnsafeParallelList<T>*)ptr->Ptr)->Clear();
            }
        }

        /// <summary>Returns a parallel writer backed by this bus.</summary>
        public UnsafeParallelList<T>.ThreadWriter AsParallelWriter<T>() where T : unmanaged
        {
            var hash = Hash<T>.Value.Data;

            if (_parallelMap->TryGetRefValue(hash, out var ptr))
            {
                return ((UnsafeParallelList<T>*)ptr->Ptr)->AsThreadWriter();
            }

            return default;
        }

        public UnsafeParallelList<T>.ThreadReader AsParallelReader<T>()
            where T : unmanaged
        {
            var hash = Hash<T>.Value.Data;

            if (_parallelMap->TryGetRefValue(hash, out var ptr))
            {
                return ((UnsafeParallelList<T>*)ptr->Ptr)->AsThreadReader();
            }

            return default;
        }

        /// <summary>Per-type static hash cache. Populated by <see cref="Register{T}"/>; read in Burst code.</summary>
        public static class Hash<T> where T : unmanaged
        {
            public static readonly SharedStatic<ulong> Value = SharedStatic<ulong>.GetOrCreate<ParallelEventBus, T>();
        }
    }
}