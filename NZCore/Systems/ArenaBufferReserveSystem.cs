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
            public EntityQuery StagingQuery;
            public TypeIndex StagingTypeIndex;

            /// <summary>Order version filtered: only chunks that structurally changed can hold new records.</summary>
            public EntityQuery ChangedQuery;

            /// <summary>Unfiltered, for teardown and the leak count.</summary>
            public EntityQuery AllQuery;

            public TypeIndex RefTypeIndex;
            public IntPtr Arena;
            public ArenaAllocatorMode Mode;
            public int ElementSize;
        }

        private UnsafeList<ArenaQuerySet> _sets;
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

            var all = new NativeList<ComponentType>(2, Allocator.Temp);

            for (var i = 0; i < registrations->Length; i++)
            {
                var registration = (*registrations)[i];

                all.Clear();
                all.Add(ComponentType.ReadWrite(registration.RefTypeIndex));

                var changedQuery = new EntityQueryBuilder(Allocator.Temp).WithAll(ref all).Build(ref state);
                changedQuery.SetOrderVersionFilter();

                // Disabled entities are included deliberately. A record that was reserved while enabled keeps
                // its block after SetEnabled(false), and a default query would not see it - so teardown would
                // walk past a live block, and the leak check would count the block but not the record holding
                // it. Prefabs stay excluded: they are never reserved (StagingQuery skips them too), so adding
                // them would only pad the denominator and mask a real leak.
                var allQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll(ref all)
                    .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                    .Build(ref state);

                all.Add(ComponentType.ReadOnly(registration.StagingTypeIndex));
                var stagingQuery = new EntityQueryBuilder(Allocator.Temp).WithAll(ref all).Build(ref state);
                all.RemoveAt(1);

                _sets.Add(new ArenaQuerySet
                {
                    StagingQuery = stagingQuery,
                    StagingTypeIndex = registration.StagingTypeIndex,
                    ChangedQuery = changedQuery,
                    AllQuery = allQuery,
                    RefTypeIndex = registration.RefTypeIndex,
                    Arena = registration.Arena,
                    Mode = registration.Mode,
                    ElementSize = registration.ElementSize
                });
            }

            all.Dispose();

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

            ArenaBufferRegistry.RemoveReserveSystem();
        }

        public void OnUpdate(ref SystemState state)
        {
            for (var i = 0; i < _sets.Length; i++)
            {
                var set = _sets[i];

                if (!set.StagingQuery.IsEmpty)
                {
                    InitializeStaged(ref state, ref set);
                }

                if (!set.ChangedQuery.IsEmpty)
                {
                    Reserve(ref state, ref set);
                }

                CheckForLeakedBlocks(ref set);
            }
        }

        private static void InitializeStaged(ref SystemState state, ref ArenaQuerySet set)
        {
            state.Dependency.Complete();

            var entityManager = state.EntityManager;
            using var entities = set.StagingQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var refData = (ArenaBufferRefData*)entityManager.GetComponentDataRaw(
                    set.RefTypeIndex,
                    entity,
                    false);
                var staging = (BufferHeaderExposed*)entityManager.GetComponentDataRaw(
                    set.StagingTypeIndex,
                    entity,
                    true);

                var requestedCapacity = Math.Max(refData->Capacity, staging->Capacity);

                // ECB can add an untyped ref component but cannot record its generated Request value.
                if (refData->Block == IntPtr.Zero && refData->Length == 0 && refData->Capacity == 0)
                {
                    refData->Handle = ArenaBufferRefData.Unreserved;
                }

                ArenaBufferDispatch.Reallocate(
                    set.Arena,
                    set.Mode,
                    ref *refData,
                    requestedCapacity,
                    set.ElementSize);

                if (staging->Length > 0)
                {
                    UnsafeUtility.MemCpy(
                        ArenaBufferDispatch.Resolve(set.Arena, set.Mode, *refData),
                        BufferHeaderExposed.GetElementPointer(staging),
                        (long)staging->Length * set.ElementSize);
                }

                refData->Length = staging->Length;
            }

            entityManager.RemoveComponent(
                set.StagingQuery,
                ComponentType.FromTypeIndex(set.StagingTypeIndex));
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

                    ArenaBufferDispatch.Free(set.Arena, set.Mode, refData.Block, refData.Capacity, set.ElementSize);

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
            if (ArenaBufferRegistry.ReserveSystemCount != 1)
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
