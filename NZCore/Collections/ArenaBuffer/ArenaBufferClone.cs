// <copyright project="NZCore" file="ArenaBufferClone.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace NZCore
{
    /// <summary>
    /// The arena equivalent of what ECS does for DynamicBuffers when an entity is cloned.
    ///
    /// A buffer component gets its external allocation duplicated by the engine itself: the archetype keeps
    /// buffer components in their own range and <c>ChunkDataUtility.ReplicateComponents</c> runs a second pass
    /// over it calling <c>BufferHeader.Assign</c>, so a clone owns its memory the moment
    /// <c>Instantiate</c> returns. A <see cref="ArenaBufferRefData"/> is an ordinary <c>IComponentData</c>, so
    /// none of that happens - ECS copies the record verbatim and the clone points at somebody else's block, or
    /// at no block at all.
    ///
    /// Call <see cref="FixupInstantiated"/> straight after instantiating, on the main thread, and the entities
    /// come out with their own memory and are safe to read and write immediately - including through
    /// <c>GetUnsafePtr</c>, which is how stat code reaches them.
    /// </summary>
    public static unsafe class ArenaBufferClone
    {
        /// <summary>
        /// Gives every entity in <paramref name="entities"/> its own arena block for each registered element
        /// type it carries, and returns how many records were fixed up.
        ///
        /// Three cases, per record:
        /// <list type="bullet">
        /// <item>reserved - the block is the source entity's, so a fresh one is allocated and the contents
        /// copied across. The source's block is deliberately not freed.</item>
        /// <item>unreserved with staging - the baked elements are copied into a new block and the staging
        /// buffer is dropped, so <c>ArenaBufferReserveSystem</c> will not later overwrite what the caller
        /// writes next.</item>
        /// <item>unreserved without staging - a block of the requested capacity, left uninitialized.</item>
        /// </list>
        ///
        /// Structural: dropping staging moves entities between chunks, so any lookup or type handle held
        /// across this call has to be rebuilt afterwards.
        /// </summary>
        public static int FixupInstantiated(ref SystemState state, NativeArray<Entity> entities, bool includeLinkedEntityGroup = true)
        {
            // Raw component access on the main thread, so anything still writing these records has to be done.
            state.CompleteDependency();

            return FixupInstantiated(state.EntityManager, entities, includeLinkedEntityGroup);
        }

        /// <inheritdoc cref="FixupInstantiated(ref SystemState, NativeArray{Entity}, bool)"/>
        public static int FixupInstantiated(EntityManager entityManager, NativeArray<Entity> entities, bool includeLinkedEntityGroup = true)
        {
            var registrations = ArenaBufferRegistry.GetRegistrations();

            if (registrations == null || registrations->Length == 0 || entities.Length == 0)
            {
                return 0;
            }

            var expanded = includeLinkedEntityGroup
                ? ExpandLinkedEntityGroups(entityManager, entities)
                : default;

            var targets = expanded.IsCreated ? expanded.AsArray() : entities;
            var fixedUp = 0;

            var staged = new NativeList<Entity>(targets.Length, Allocator.TempJob);

            for (var r = 0; r < registrations->Length; r++)
            {
                var registration = (*registrations)[r];
                var refType = ComponentType.FromTypeIndex(registration.RefTypeIndex);
                var stagingType = ComponentType.FromTypeIndex(registration.StagingTypeIndex);

                staged.Clear();

                for (var i = 0; i < targets.Length; i++)
                {
                    var entity = targets[i];

                    if (!entityManager.HasComponent(entity, refType))
                    {
                        continue;
                    }

                    var refData = (ArenaBufferRefData*)entityManager.GetComponentDataRaw(
                        registration.RefTypeIndex,
                        entity,
                        false);

                    if (refData->IsReserved)
                    {
                        DetachSharedBlock(ref registration, refData);
                        fixedUp++;
                        continue;
                    }

                    if (entityManager.HasComponent(entity, stagingType))
                    {
                        var stagingHeader = (BufferHeaderExposed*)entityManager.GetComponentDataRaw(
                            registration.StagingTypeIndex,
                            entity,
                            true);

                        InitializeFromStaging(ref registration, refData, stagingHeader);
                        staged.Add(entity);
                        fixedUp++;
                        continue;
                    }

                    if (refData->Capacity > 0)
                    {
                        ArenaBufferDispatch.Reallocate(
                            registration.Arena,
                            registration.Mode,
                            ref *refData,
                            refData->Capacity,
                            registration.ElementSize);

                        fixedUp++;
                    }
                }

                if (staged.Length > 0)
                {
                    // After the loop rather than inside it: this is a structural change, and it would
                    // invalidate the raw record pointers the loop is still walking.
                    entityManager.RemoveComponent(staged.AsArray(), stagingType);
                }
            }

            staged.Dispose();

            if (expanded.IsCreated)
            {
                expanded.Dispose();
            }

            return fixedUp;
        }

        /// <summary>
        /// Instantiating a prefab clones its whole linked group, and an entity's stat buffers routinely live
        /// on a linked child rather than the root - so fixing up only the roots would leave exactly the
        /// records the caller is about to write through.
        /// </summary>
        private static NativeList<Entity> ExpandLinkedEntityGroups(EntityManager entityManager, NativeArray<Entity> roots)
        {
            var expanded = new NativeList<Entity>(roots.Length * 2, Allocator.TempJob);

            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                expanded.Add(root);

                if (!entityManager.HasBuffer<LinkedEntityGroup>(root))
                {
                    continue;
                }

                var linkedGroup = entityManager.GetBuffer<LinkedEntityGroup>(root, true);

                for (var g = 0; g < linkedGroup.Length; g++)
                {
                    if (linkedGroup[g].Value != root)
                    {
                        expanded.Add(linkedGroup[g].Value);
                    }
                }
            }

            return expanded;
        }

        /// <summary>
        /// The record still points at the block of the entity it was cloned from. Stamping it unreserved first
        /// is what makes <c>Reallocate</c> take its allocate-only path - otherwise it would hand the source's
        /// block back to the free lists while the source is still using it.
        /// </summary>
        private static void DetachSharedBlock(ref ArenaTypeRegistration registration, ArenaBufferRefData* refData)
        {
            var sourceBlock = ArenaBufferDispatch.Resolve(registration.Arena, registration.Mode, *refData);
            var length = refData->Length;
            var capacity = refData->Capacity;

            refData->Handle = ArenaBufferRefData.Unreserved;

            ArenaBufferDispatch.Reallocate(
                registration.Arena,
                registration.Mode,
                ref *refData,
                math.max(capacity, length),
                registration.ElementSize);

            if (length > 0)
            {
                UnsafeUtility.MemCpy(
                    ArenaBufferDispatch.Resolve(registration.Arena, registration.Mode, *refData),
                    sourceBlock,
                    (long)length * registration.ElementSize);
            }

            refData->Length = length;
        }

        private static void InitializeFromStaging(
            ref ArenaTypeRegistration registration,
            ArenaBufferRefData* refData,
            BufferHeaderExposed* stagingHeader)
        {
            var requestedCapacity = math.max(refData->Capacity, stagingHeader->Capacity);

            ArenaBufferDispatch.Reallocate(
                registration.Arena,
                registration.Mode,
                ref *refData,
                requestedCapacity,
                registration.ElementSize);

            if (stagingHeader->Length > 0)
            {
                UnsafeUtility.MemCpy(
                    ArenaBufferDispatch.Resolve(registration.Arena, registration.Mode, *refData),
                    BufferHeaderExposed.GetElementPointer(stagingHeader),
                    (long)stagingHeader->Length * registration.ElementSize);
            }

            refData->Length = stagingHeader->Length;
        }
    }
}