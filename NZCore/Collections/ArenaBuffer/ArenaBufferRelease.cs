// <copyright project="NZCore" file="ArenaBufferRelease.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    /// <summary>
    /// Frees the arena blocks of entities that are about to be destroyed, without knowing which element
    /// types they carry.
    ///
    /// Arena buffers deliberately have no cleanup component: a destroy pipeline is expected to release the
    /// blocks itself while the <c>*Ref</c> components are still on the entities. Create this once in
    /// OnCreate, <see cref="Update"/> it each frame, and call <see cref="ReleaseChunk"/> for every chunk of
    /// doomed entities before the components go away.
    ///
    /// Released records are stamped back to <see cref="ArenaBufferRefData.Unreserved"/>, so releasing the
    /// same chunk twice is harmless.
    /// </summary>
    public unsafe struct ArenaBufferReleaseHandles : IDisposable
    {
        private UnsafeList<DynamicComponentTypeHandle> _handles;
        private UnsafeList<IntPtr> _arenas;
        private UnsafeList<ArenaAllocatorMode> _modes;
        private UnsafeList<int> _elementSizes;

        public bool IsCreated => _handles.IsCreated;

        /// <summary>Number of registered element types this instance can release.</summary>
        public int TypeCount => _handles.IsCreated ? _handles.Length : 0;

        public static ArenaBufferReleaseHandles Create(ref SystemState state)
        {
            var registrations = ArenaBufferRegistry.GetRegistrations();
            var count = registrations == null ? 0 : registrations->Length;

            var result = new ArenaBufferReleaseHandles
            {
                _handles = new UnsafeList<DynamicComponentTypeHandle>(count, Allocator.Persistent),
                _arenas = new UnsafeList<IntPtr>(count, Allocator.Persistent),
                _modes = new UnsafeList<ArenaAllocatorMode>(count, Allocator.Persistent),
                _elementSizes = new UnsafeList<int>(count, Allocator.Persistent)
            };

            for (var i = 0; i < count; i++)
            {
                var registration = (*registrations)[i];

                result._handles.Add(state.GetDynamicComponentTypeHandle(ComponentType.ReadWrite(registration.RefTypeIndex)));
                result._arenas.Add(registration.Arena);
                result._modes.Add(registration.Mode);
                result._elementSizes.Add(registration.ElementSize);
            }

            return result;
        }

        public void Update(ref SystemState state)
        {
            for (var i = 0; i < _handles.Length; i++)
            {
                var handle = _handles[i];
                handle.Update(ref state);
                _handles[i] = handle;
            }
        }

        /// <summary>
        /// Frees every arena block held by the chunk, across all registered element types.
        /// Returns how many blocks were freed.
        ///
        /// Releases the <b>whole</b> chunk. When the doomed entities are selected by an
        /// <see cref="IEnableableComponent"/> - which is the normal case, since a chunk matched by such a
        /// query still holds entities that are staying alive - use the
        /// <see cref="ReleaseChunk(in ArchetypeChunk, in EnabledMask)"/> overload instead.
        /// </summary>
        public int ReleaseChunk(in ArchetypeChunk chunk)
        {
            return ReleaseChunk(chunk, default, false);
        }

        /// <summary>
        /// Frees the arena blocks of the entities the mask marks as enabled, leaving the rest of the chunk
        /// alone. <paramref name="enabledMask"/> comes from the component that selects the doomed entities,
        /// for example <c>chunk.GetEnabledMask(ref destroyEntityHandle)</c>.
        /// </summary>
        public int ReleaseChunk(in ArchetypeChunk chunk, in EnabledMask enabledMask)
        {
            return ReleaseChunk(chunk, enabledMask, true);
        }

        private int ReleaseChunk(in ArchetypeChunk chunk, in EnabledMask enabledMask, bool useMask)
        {
            var freed = 0;

            for (var i = 0; i < _handles.Length; i++)
            {
                var handle = _handles[i];

                if (!chunk.Has(ref handle))
                {
                    _handles[i] = handle;
                    continue;
                }

                var refs = chunk.GetDynamicComponentDataArrayReinterpret<ArenaBufferRefData>(ref handle, UnsafeUtility.SizeOf<ArenaBufferRefData>());
                var arena = _arenas[i];
                var mode = _modes[i];
                var elementSize = _elementSizes[i];
                var entityCount = chunk.Count;

                for (var e = 0; e < entityCount; e++)
                {
                    if (useMask && !enabledMask[e])
                    {
                        continue;
                    }

                    var refData = refs[e];

                    if (!refData.IsReserved)
                    {
                        continue;
                    }

                    ArenaBufferDispatch.Free(arena, mode, refData.Handle, refData.Capacity, elementSize);

                    refData.Handle = ArenaBufferRefData.Unreserved;
                    refData.Length = 0;
                    refs[e] = refData;

                    freed++;
                }

                // The handle carries a lookup cache that Has() and the reinterpret both advance.
                _handles[i] = handle;
            }

            return freed;
        }

        public void Dispose()
        {
            if (_handles.IsCreated)
            {
                _handles.Dispose();
            }

            if (_arenas.IsCreated)
            {
                _arenas.Dispose();
            }

            if (_modes.IsCreated)
            {
                _modes.Dispose();
            }

            if (_elementSizes.IsCreated)
            {
                _elementSizes.Dispose();
            }
        }
    }
}
