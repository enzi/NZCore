// <copyright project="NZCore" file="ArenaBufferReserveSystem.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    /// <summary>
    /// Turns unreserved <c>*Ref</c> records into real arena blocks, frees this World's blocks when it is torn
    /// down, and reports blocks that were leaked.
    ///
    /// One system serves every registered element type. Systems cannot be generic, but every generated
    /// <c>*Ref</c> component shares the <see cref="ArenaBufferRefData"/> layout, so a
    /// <see cref="DynamicComponentTypeHandle"/> plus a reinterpret is enough to do the work without knowing
    /// the concrete types. It makes no structural changes at all - reserving only writes an offset into a
    /// record that is already there.
    ///
    /// Releasing on entity destruction is deliberately <b>not</b> this system's job. There is no cleanup
    /// component, so a destroy pipeline has to free the blocks itself with
    /// <see cref="ArenaBufferReleaseHandles"/> while the components are still on the entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public unsafe partial struct ArenaBufferReserveSystem : ISystem
    {
        private struct ArenaQuerySet
        {
            /// <summary>Order version filtered: only chunks that structurally changed can hold new records.</summary>
            public EntityQuery ChangedQuery;

            /// <summary>Unfiltered, for teardown and the leak count.</summary>
            public EntityQuery AllQuery;

            public TypeIndex RefTypeIndex;
            public IntPtr Arena;
            public ArenaAllocatorMode Mode;
            public int ElementSize;
        }

        /// <summary>One element type participating in the shared arena, in the order its run is laid out.</summary>
        private struct SharedType
        {
            public TypeIndex RefTypeIndex;
            public int ElementSize;
        }

        private UnsafeList<ArenaQuerySet> _sets;

        // The shared mode cannot be served per type: one page holds several types, so a chunk has to be
        // visited once with every shared type in hand rather than once per type.
        private UnsafeList<SharedType> _sharedTypes;
        private EntityQuery _sharedQuery;

        // Unfiltered: growth makes no structural change, so a chunk whose buffers have outgrown their page
        // never bumps its order version and would never be seen by the filtered query.
        private EntityQuery _sharedAllQuery;

        private IntPtr _sharedArena;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var registrations = ArenaBufferRegistry.GetRegistrations();

            if (registrations == null || registrations->Length == 0)
            {
                state.Enabled = false;
                return;
            }

            _sets = new UnsafeList<ArenaQuerySet>(registrations->Length, Allocator.Persistent);
            _sharedTypes = new UnsafeList<SharedType>(registrations->Length, Allocator.Persistent);

            var all = new NativeList<ComponentType>(1, Allocator.Temp);

            for (var i = 0; i < registrations->Length; i++)
            {
                var registration = (*registrations)[i];

                all.Clear();
                all.Add(ComponentType.ReadWrite(registration.RefTypeIndex));

                var changedQuery = new EntityQueryBuilder(Allocator.Temp).WithAll(ref all).Build(ref state);
                changedQuery.SetOrderVersionFilter();

                var allQuery = new EntityQueryBuilder(Allocator.Temp).WithAll(ref all).Build(ref state);

                _sets.Add(new ArenaQuerySet
                {
                    ChangedQuery = changedQuery,
                    AllQuery = allQuery,
                    RefTypeIndex = registration.RefTypeIndex,
                    Arena = registration.Arena,
                    Mode = registration.Mode,
                    ElementSize = registration.ElementSize
                });

                if (registration.Mode == ArenaAllocatorMode.SharedChunkPaged)
                {
                    _sharedTypes.Add(new SharedType
                    {
                        RefTypeIndex = registration.RefTypeIndex,
                        ElementSize = registration.ElementSize
                    });

                    _sharedArena = registration.Arena;
                }
            }

            all.Dispose();

            if (_sharedTypes.Length > 0)
            {
                var any = new NativeList<ComponentType>(_sharedTypes.Length, Allocator.Temp);

                for (var i = 0; i < _sharedTypes.Length; i++)
                {
                    any.Add(ComponentType.ReadWrite(_sharedTypes[i].RefTypeIndex));
                }

                _sharedQuery = new EntityQueryBuilder(Allocator.Temp).WithAny(ref any).Build(ref state);
                _sharedQuery.SetOrderVersionFilter();

                _sharedAllQuery = new EntityQueryBuilder(Allocator.Temp).WithAny(ref any).Build(ref state);

                any.Dispose();
            }

            ArenaBufferRegistry.AddReserveSystem();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (!_sets.IsCreated)
            {
                return;
            }

            // World teardown. Entity storage is still intact at this point, so every block this World's
            // entities hold can be handed back precisely - which matters because the arenas are process
            // global and another World's blocks must survive.
            for (var i = 0; i < _sets.Length; i++)
            {
                var set = _sets[i];
                ReleaseAll(ref state, ref set);
            }

            _sets.Dispose();
            _sharedTypes.Dispose();

            ArenaBufferRegistry.RemoveReserveSystem();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            for (var i = 0; i < _sets.Length; i++)
            {
                var set = _sets[i];

                if (!set.ChangedQuery.IsEmpty && set.Mode != ArenaAllocatorMode.SharedChunkPaged)
                {
                    if (set.Mode == ArenaAllocatorMode.ChunkPaged)
                    {
                        ReserveChunkPaged(ref state, ref set);
                    }
                    else
                    {
                        Reserve(ref state, ref set);
                    }
                }

                CheckForLeakedBlocks(ref set);
            }

            if (_sharedTypes.Length > 0)
            {
                if (!_sharedQuery.IsEmpty)
                {
                    ReserveShared(ref state);
                }

                if (!_sharedAllQuery.IsEmpty)
                {
                    ReHomeShared(ref state);
                }
            }

            CheckForLeakedSharedBlocks();
        }

        private void Reserve(ref SystemState state, ref ArenaQuerySet set)
        {
            var handle = state.GetDynamicComponentTypeHandle(ComponentType.ReadWrite(set.RefTypeIndex));
            var chunks = set.ChangedQuery.ToArchetypeChunkArray(Allocator.Temp);

            for (var c = 0; c < chunks.Length; c++)
            {
                var chunk = chunks[c];
                var refs = chunk.GetDynamicComponentDataArrayReinterpret<ArenaBufferRefData>(ref handle, UnsafeUtility.SizeOf<ArenaBufferRefData>());
                var entityCount = chunk.Count;

                for (var e = 0; e < entityCount; e++)
                {
                    var refData = refs[e];

                    if (refData.IsReserved)
                    {
                        continue;
                    }

                    ArenaBufferDispatch.Reallocate(set.Arena, set.Mode, ref refData, refData.Capacity, set.ElementSize);
                    refs[e] = refData;
                }
            }

            chunks.Dispose();
        }

        /// <summary>
        /// Reserves a whole chunk at once, which is the entire point of
        /// <see cref="ArenaAllocatorMode.ChunkPaged"/>: the chunk's records get consecutive blocks on a page of
        /// their own, so a chunk accessor can resolve that page once instead of per entity.
        ///
        /// One page means one size class, so the class is taken from the largest capacity anyone in the chunk
        /// asked for and everyone is padded up to it. Records already holding a block - entities that moved in
        /// after their old chunk was reserved - keep it and are simply skipped, which costs their new chunk a
        /// page reload when it is iterated.
        /// </summary>
        private void ReserveChunkPaged(ref SystemState state, ref ArenaQuerySet set)
        {
            var handle = state.GetDynamicComponentTypeHandle(ComponentType.ReadWrite(set.RefTypeIndex));
            var chunks = set.ChangedQuery.ToArchetypeChunkArray(Allocator.Temp);

            for (var c = 0; c < chunks.Length; c++)
            {
                var chunk = chunks[c];
                var refs = chunk.GetDynamicComponentDataArrayReinterpret<ArenaBufferRefData>(ref handle, UnsafeUtility.SizeOf<ArenaBufferRefData>());
                var entityCount = chunk.Count;

                // First pass: how many records need a block, and the class the page has to be carved at.
                var pending = 0;
                var largest = 1;

                for (var e = 0; e < entityCount; e++)
                {
                    var refData = refs[e];

                    if (refData.IsReserved)
                    {
                        continue;
                    }

                    pending++;

                    if (refData.Capacity > largest)
                    {
                        largest = refData.Capacity;
                    }
                }

                if (pending == 0)
                {
                    continue;
                }

                // Second pass: take the pages and hand out consecutive blocks off them.
                var capacity = ArenaAllocator.RoundCapacity(largest);
                var stride = ArenaBufferDispatch.ChunkPagedBlockStride(set.Arena, capacity);

                var remaining = pending;
                var cursor = 0;

                while (remaining > 0)
                {
                    var first = ArenaBufferDispatch.AllocateChunk(set.Arena, remaining, capacity, out var onThisPage);

                    for (var b = 0; b < onThisPage; b++)
                    {
                        // Skip anything already holding a block. The first pass counted exactly the records
                        // this loop writes, so the cursor cannot run past the chunk.
                        while (refs[cursor].IsReserved)
                        {
                            cursor++;
                        }

                        var refData = refs[cursor];

                        refData.Handle = first + b * stride;
                        refData.Length = 0;
                        refData.Capacity = capacity;

                        refs[cursor] = refData;
                        cursor++;
                    }

                    remaining -= onThisPage;
                }
            }

            chunks.Dispose();
        }

        /// <summary>
        /// Reserves a chunk across every shared element type at once, which is what
        /// <see cref="ArenaAllocatorMode.SharedChunkPaged"/> exists for: the chunk's buffers of all types land
        /// on the same page, laid out as one contiguous run per type so each type still iterates sequentially.
        ///
        /// Two passes. The first sizes every type's run - how many records need a block, and the block size
        /// that type needs in this chunk, padded to the largest capacity anyone asked for. The second takes
        /// pages and walks the runs into them, starting a new page whenever the next block will not fit.
        /// A chunk needing more than a page's worth simply spans several, which the buffer's page check
        /// absorbs at the cost of a reload at each boundary.
        /// </summary>
        private void ReserveShared(ref SystemState state)
        {
            var typeCount = _sharedTypes.Length;

            var handles = new NativeArray<DynamicComponentTypeHandle>(typeCount, Allocator.Temp);
            var pending = new NativeArray<int>(typeCount, Allocator.Temp);
            var blockBytes = new NativeArray<int>(typeCount, Allocator.Temp);
            var capacities = new NativeArray<int>(typeCount, Allocator.Temp);
            var groupCounts = new NativeArray<int>(typeCount, Allocator.Temp);

            for (var t = 0; t < typeCount; t++)
            {
                handles[t] = state.GetDynamicComponentTypeHandle(ComponentType.ReadWrite(_sharedTypes[t].RefTypeIndex));
            }

            var chunks = _sharedQuery.ToArchetypeChunkArray(Allocator.Temp);
            var refSize = UnsafeUtility.SizeOf<ArenaBufferRefData>();

            for (var c = 0; c < chunks.Length; c++)
            {
                var chunk = chunks[c];
                var entityCount = chunk.Count;
                var totalBytes = 0L;

                // Pass one: size each type's run in this chunk.
                for (var t = 0; t < typeCount; t++)
                {
                    pending[t] = 0;

                    var handle = handles[t];

                    if (!chunk.Has(ref handle))
                    {
                        handles[t] = handle;
                        continue;
                    }

                    var refs = chunk.GetDynamicComponentDataArrayReinterpret<ArenaBufferRefData>(ref handle, refSize);
                    handles[t] = handle;

                    var count = 0;
                    var largest = 1;

                    for (var e = 0; e < entityCount; e++)
                    {
                        var refData = refs[e];

                        if (refData.IsReserved)
                        {
                            continue;
                        }

                        count++;

                        if (refData.Capacity > largest)
                        {
                            largest = refData.Capacity;
                        }
                    }

                    if (count == 0)
                    {
                        continue;
                    }

                    var capacity = ArenaAllocator.RoundCapacity(largest);

                    pending[t] = count;
                    capacities[t] = capacity;
                    blockBytes[t] = SharedArenaAllocator.BlockBytes(capacity, _sharedTypes[t].ElementSize);

                    totalBytes += (long)count * blockBytes[t];
                }

                if (totalBytes == 0)
                {
                    continue;
                }

                // Pass two: tile the chunk into pages the way an entity chunk tiles memory. Each 16 KB page
                // takes as many entities as fit and lays every type's run for *those* entities side by side,
                // so reading several types of one entity stays inside a page. The alternative - one run per
                // type across the whole chunk - keeps a single type's run contiguous but puts the types far
                // apart, which is what a multi type read pays for.
                var perEntityBytes = 0;

                for (var t = 0; t < typeCount; t++)
                {
                    if (pending[t] > 0)
                    {
                        perEntityBytes += blockBytes[t];
                    }
                }

                var entitiesPerPage = SharedArenaAllocator.MaxPageBytes / perEntityBytes;

                if (entitiesPerPage < 1)
                {
                    // One entity's types do not fit a page; give it a page of its own, oversized.
                    entitiesPerPage = 1;
                }

                var arena = (SharedArenaAllocator*)_sharedArena;
                var placed = 0;

                while (placed < entityCount)
                {
                    var groupEntities = entityCount - placed;

                    if (groupEntities > entitiesPerPage)
                    {
                        groupEntities = entitiesPerPage;
                    }

                    // Size the page to what this group actually needs, counting only records still unreserved.
                    var pageBytes = 0;

                    for (var t = 0; t < typeCount; t++)
                    {
                        if (pending[t] == 0)
                        {
                            continue;
                        }

                        var handle = handles[t];
                        var refs = chunk.GetDynamicComponentDataArrayReinterpret<ArenaBufferRefData>(ref handle, refSize);
                        handles[t] = handle;

                        var needed = 0;

                        for (var e = placed; e < placed + groupEntities; e++)
                        {
                            if (!refs[e].IsReserved)
                            {
                                needed++;
                            }
                        }

                        groupCounts[t] = needed;
                        pageBytes += needed * blockBytes[t];
                    }

                    if (pageBytes == 0)
                    {
                        placed += groupEntities;
                        continue;
                    }

                    var pageBase = arena->AllocateChunkPage(pageBytes, out _);
                    var pageOffset = 0;

                    for (var t = 0; t < typeCount; t++)
                    {
                        if (groupCounts[t] == 0)
                        {
                            continue;
                        }

                        var handle = handles[t];
                        var refs = chunk.GetDynamicComponentDataArrayReinterpret<ArenaBufferRefData>(ref handle, refSize);
                        handles[t] = handle;

                        var stride = blockBytes[t];
                        var capacity = capacities[t];
                        var slot = 0;

                        for (var e = placed; e < placed + groupEntities; e++)
                        {
                            var refData = refs[e];

                            if (refData.IsReserved)
                            {
                                continue;
                            }

                            refData.Handle = pageBase + pageOffset + slot * stride;
                            refData.Length = 0;
                            refData.Capacity = capacity;

                            refs[e] = refData;
                            slot++;
                        }

                        arena->AccountChunkBlocks(groupCounts[t], stride);
                        pageOffset += groupCounts[t] * stride;
                    }

                    placed += groupEntities;
                }
            }

            chunks.Dispose();
            groupCounts.Dispose();
            capacities.Dispose();
            blockBytes.Dispose();
            pending.Dispose();
            handles.Dispose();
        }

        /// <summary>
        /// Restores the tiled page layout after buffers have grown out of it.
        ///
        /// A buffer that outgrows the stride its chunk page was laid out at takes an ordinary free list block
        /// somewhere else, and from then on reads take the fallback resolve. This pass finds those chunks and
        /// re-lays the whole chunk: a fresh page sized to what each type needs *now*, every buffer copied into
        /// it, every handle rewritten. The layout is the same as reservation produces - one contiguous run per
        /// type, in type order - only the strides are bigger.
        ///
        /// The cost is a whole chunk copy for one entity's growth, paid again each time a type's largest
        /// buffer crosses a size class. It is bounded rather than continuous: once sizes settle, no chunk
        /// re-homes again. That amortisation is the thing worth measuring.
        ///
        /// This cannot live in the allocator. <c>Reallocate</c> sees one record and has no access to the
        /// chunk, its siblings, or the other types, so rewriting a chunk's layout is only possible from here.
        /// </summary>
        private void ReHomeShared(ref SystemState state)
        {
            var typeCount = _sharedTypes.Length;

            var handles = new NativeArray<DynamicComponentTypeHandle>(typeCount, Allocator.Temp);
            var present = new NativeArray<bool>(typeCount, Allocator.Temp);
            var blockBytes = new NativeArray<int>(typeCount, Allocator.Temp);
            var capacities = new NativeArray<int>(typeCount, Allocator.Temp);
            var groupCounts = new NativeArray<int>(typeCount, Allocator.Temp);

            for (var t = 0; t < typeCount; t++)
            {
                handles[t] = state.GetDynamicComponentTypeHandle(ComponentType.ReadWrite(_sharedTypes[t].RefTypeIndex));
            }

            var chunks = _sharedAllQuery.ToArchetypeChunkArray(Allocator.Temp);
            var refSize = UnsafeUtility.SizeOf<ArenaBufferRefData>();
            var arena = (SharedArenaAllocator*)_sharedArena;

            for (var c = 0; c < chunks.Length; c++)
            {
                var chunk = chunks[c];
                var entityCount = chunk.Count;

                if (entityCount == 0)
                {
                    continue;
                }

                var broken = false;
                var complete = true;
                var perEntityBytes = 0;

                // Pass one: what does each type need now, and has growth broken the layout? With a tiled
                // layout a chunk legitimately spans many pages, so "all on one page" is no longer the test -
                // what marks a grown buffer is a capacity above its type's uniform size.
                for (var t = 0; t < typeCount; t++)
                {
                    present[t] = false;

                    var handle = handles[t];

                    if (!chunk.Has(ref handle))
                    {
                        handles[t] = handle;
                        continue;
                    }

                    var refs = chunk.GetDynamicComponentDataArrayReinterpret<ArenaBufferRefData>(ref handle, refSize);
                    handles[t] = handle;

                    var largest = 1;
                    var reserved = 0;

                    for (var e = 0; e < entityCount; e++)
                    {
                        var refData = refs[e];

                        if (!refData.IsReserved)
                        {
                            continue;
                        }

                        reserved++;

                        if (refData.Capacity > largest)
                        {
                            largest = refData.Capacity;
                        }
                    }

                    if (reserved != entityCount)
                    {
                        // Unreserved records are ReserveShared's job, not this pass's.
                        complete = false;
                        break;
                    }

                    for (var e = 0; e < entityCount; e++)
                    {
                        if (refs[e].Capacity != largest)
                        {
                            broken = true;
                        }
                    }

                    present[t] = true;
                    capacities[t] = largest;
                    blockBytes[t] = SharedArenaAllocator.BlockBytes(largest, _sharedTypes[t].ElementSize);
                    perEntityBytes += blockBytes[t];
                }

                if (!complete || !broken || perEntityBytes == 0)
                {
                    continue;
                }

                // Pass two: re-tile exactly as reservation would, copying the live elements across and
                // handing the old blocks back.
                var entitiesPerPage = SharedArenaAllocator.MaxPageBytes / perEntityBytes;

                if (entitiesPerPage < 1)
                {
                    entitiesPerPage = 1;
                }

                var placed = 0;

                while (placed < entityCount)
                {
                    var groupEntities = entityCount - placed;

                    if (groupEntities > entitiesPerPage)
                    {
                        groupEntities = entitiesPerPage;
                    }

                    var pageBytes = 0;

                    for (var t = 0; t < typeCount; t++)
                    {
                        groupCounts[t] = present[t] ? groupEntities : 0;
                        pageBytes += groupCounts[t] * blockBytes[t];
                    }

                    var pageBase = arena->AllocateChunkPage(pageBytes, out _);
                    var pageOffset = 0;

                    for (var t = 0; t < typeCount; t++)
                    {
                        if (!present[t])
                        {
                            continue;
                        }

                        var handle = handles[t];
                        var refs = chunk.GetDynamicComponentDataArrayReinterpret<ArenaBufferRefData>(ref handle, refSize);
                        handles[t] = handle;

                        var stride = blockBytes[t];
                        var capacity = capacities[t];
                        var elementSize = _sharedTypes[t].ElementSize;
                        var slot = 0;

                        for (var e = placed; e < placed + groupEntities; e++)
                        {
                            var refData = refs[e];

                            var oldHandle = refData.Handle;
                            var oldBytes = SharedArenaAllocator.BlockBytes(refData.Capacity, elementSize);
                            var newHandle = pageBase + pageOffset + slot * stride;

                            if (refData.Length > 0)
                            {
                                UnsafeUtility.MemCpy(arena->Resolve(newHandle), arena->Resolve(oldHandle), (long)refData.Length * elementSize);
                            }

                            arena->Free(oldHandle, oldBytes);

                            refData.Handle = newHandle;
                            refData.Capacity = capacity;
                            refs[e] = refData;

                            slot++;
                        }

                        arena->AccountChunkBlocks(groupCounts[t], stride);
                        pageOffset += groupCounts[t] * stride;
                    }

                    placed += groupEntities;
                }
            }

            chunks.Dispose();
            groupCounts.Dispose();
            capacities.Dispose();
            blockBytes.Dispose();
            present.Dispose();
            handles.Dispose();
        }

        /// <summary>
        /// The shared arena holds every shared type's blocks, so a leak only shows against the total: the sum
        /// of the live records of all the types sharing it.
        /// </summary>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private void CheckForLeakedSharedBlocks()
        {
            if (_sharedTypes.Length == 0 || ArenaBufferRegistry.ReserveSystemCount != 1)
            {
                return;
            }

            var recordCount = 0;

            for (var i = 0; i < _sets.Length; i++)
            {
                var set = _sets[i];

                if (set.Mode == ArenaAllocatorMode.SharedChunkPaged)
                {
                    recordCount += set.AllQuery.CalculateEntityCount();
                }
            }

            var liveBlocks = ((SharedArenaAllocator*)_sharedArena)->LiveBlocks;

            if (liveBlocks <= recordCount)
            {
                return;
            }

            Unity.Debug.LogError(
                $"ArenaBuffer leak: the shared arena holds {liveBlocks} blocks but only {recordCount} components across all shared element types reference one. Release blocks with ArenaBufferReleaseHandles.ReleaseChunk before the component goes away.");
        }

        private void ReleaseAll(ref SystemState state, ref ArenaQuerySet set)
        {
            // EntityManager rather than SystemState: this runs during teardown, where registering another
            // reader/writer on a system that is going away is not appropriate.
            var handle = state.EntityManager.GetDynamicComponentTypeHandle(ComponentType.ReadWrite(set.RefTypeIndex));
            var chunks = set.AllQuery.ToArchetypeChunkArray(Allocator.Temp);

            for (var c = 0; c < chunks.Length; c++)
            {
                var chunk = chunks[c];
                var refs = chunk.GetDynamicComponentDataArrayReinterpret<ArenaBufferRefData>(ref handle, UnsafeUtility.SizeOf<ArenaBufferRefData>());
                var entityCount = chunk.Count;

                for (var e = 0; e < entityCount; e++)
                {
                    var refData = refs[e];

                    if (!refData.IsReserved)
                    {
                        continue;
                    }

                    ArenaBufferDispatch.Free(set.Arena, set.Mode, refData.Handle, refData.Capacity, set.ElementSize);

                    refData.Handle = ArenaBufferRefData.Unreserved;
                    refData.Length = 0;
                    refs[e] = refData;
                }
            }

            chunks.Dispose();
        }

        /// <summary>
        /// Every live block belongs to exactly one <c>*Ref</c> component, so a mismatch means a record was
        /// removed without its block being released - the one failure mode the design cannot recover from.
        ///
        /// Only checked when a single reserve system exists: arenas are shared across Worlds, so a second
        /// World's legitimate blocks would otherwise read as leaks.
        /// </summary>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private void CheckForLeakedBlocks(ref ArenaQuerySet set)
        {
            if (ArenaBufferRegistry.ReserveSystemCount != 1 || set.Mode == ArenaAllocatorMode.SharedChunkPaged)
            {
                // Shared types are checked together by CheckForLeakedSharedBlocks: one type's record count
                // says nothing about an arena holding every shared type's blocks.
                return;
            }

            var liveBlocks = ArenaBufferDispatch.LiveBlocks(set.Arena, set.Mode);
            var recordCount = set.AllQuery.CalculateEntityCount();

            if (liveBlocks <= recordCount)
            {
                return;
            }

            // Unity.Debug with an interpolated FixedString is the Burst compatible logging path.
            Unity.Debug.LogError(
                $"ArenaBuffer leak: the {TypeManager.GetTypeNameFixed(set.RefTypeIndex)} arena holds {liveBlocks} blocks but only {recordCount} components reference one. Release blocks with ArenaBufferReleaseHandles.ReleaseChunk before the component goes away.");
        }
    }
}
