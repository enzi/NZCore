// <copyright project="Unity.Entities" file="InstantiateInternals.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    public static unsafe class InstantiateInternals
    {
        private struct RemapChunk
        {
            public UnityEntities.ChunkIndex Chunk;
            public int IndexInChunk;
            public int AllocatedCount;
            public int InstanceBeginIndex;
        }

        public static void Instantiate(EntityManager entityManager, Entity srcEntity, Entity* outputEntities, int instanceCount, TypeIndex* typeIndices,
            void** sharedComponents, void** defaultSharedComponents, int sharedComponentCount)
        {
#if UNITY_7000_0_OR_NEWER
            var access = entityManager.GetCheckedEntityDataAccess(srcEntity);
#else
            var access = entityManager.GetCheckedEntityDataAccess();
#endif
            var ecs = access->EntityComponentStore;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (instanceCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(instanceCount), "instanceCount must be non-negative");
            }
#endif
            ecs->AssertEntitiesExist(&srcEntity, 1);
#if UNITY_6000_6_OR_NEWER
            ecs->AssertCanInstantiateEntities(srcEntity);
#else
            ecs->AssertCanInstantiateEntities(srcEntity, outputEntities, instanceCount);
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            for (var i = 0; i < sharedComponentCount; i++)
            {
                if (!ecs->HasComponent(srcEntity, typeIndices[i], out _))
                {
                    throw new ArgumentException("srcEntity doesn't have the shared component that the instances should get.");
                }
            }
#endif

            access->PrepareForAdditiveStructuralChanges(ecs->GetArchetype(srcEntity)->InstantiateArchetype);
            var changes = access->BeginAdditiveStructuralChanges();

            var sharedComponentIndices = stackalloc int[sharedComponentCount];
            for (var i = 0; i < sharedComponentCount; i++)
            {
                sharedComponentIndices[i] = access->InsertSharedComponent_Unmanaged(typeIndices[i], 0, sharedComponents[i], defaultSharedComponents[i]);
            }

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
            {
                access->StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.CreateEntity, in access->m_WorldUnmanaged);
            }
#endif

            InstantiateGroup(ecs, srcEntity, outputEntities, instanceCount, typeIndices, sharedComponentIndices, sharedComponentCount);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
            {
                access->StructuralChangesRecorder.End();
            }
#endif

#if ((!UNITY_7000_0_OR_NEWER && UNITY_INCLUDE_INSTRUMENTATION) || !UNITY_6000_6_OR_NEWER && UNITY_EDITOR) && !DISABLE_ENTITIES_JOURNALING
            if (ecs->m_RecordToJournal != 0)
            {
                access->JournalAddRecord_CreateEntity(default, outputEntities, instanceCount);
            }
#endif

            access->EndStructuralChanges(ref changes);
        }

        private static void InstantiateGroup(UnityEntities.EntityComponentStore* ecs, Entity srcEntity, Entity* outputRootEntities, int instanceCount,
            TypeIndex* typeIndices, int* sharedComponentIndices, int sharedComponentCount)
        {
            var srcEntities = &srcEntity;
            var srcEntityCount = 1;
            var linkedGroupType = TypeManager.GetTypeIndex<LinkedEntityGroup>();

            if (ecs->HasComponent(srcEntity, linkedGroupType, out _))
            {
                var header = (UnityEntities.BufferHeader*)ecs->GetComponentDataWithTypeRO(srcEntity, linkedGroupType);
                srcEntities = (Entity*)UnityEntities.BufferHeader.GetElementPointer(header);
                srcEntityCount = header->Length;
            }

            var totalCount = srcEntityCount * instanceCount;
            var allocationSize = sizeof(Entity) * totalCount + sizeof(RemapChunk) * totalCount + sizeof(Entity) * instanceCount;
            var entityRemap = (Entity*)UnsafeUtility.Malloc(allocationSize, 16, Allocator.Temp);
            var remapChunks = (RemapChunk*)(entityRemap + totalCount);
            var outputEntities = (Entity*)(remapChunks + totalCount);
            var remapChunksCount = 0;

            for (var i = 0; i < srcEntityCount; i++)
            {
                remapChunksCount = InstantiateOne(ecs, srcEntities[i], outputEntities, instanceCount, remapChunks, remapChunksCount, typeIndices,
                    sharedComponentIndices, sharedComponentCount);

                for (var r = 0; r < instanceCount; r++)
                {
                    entityRemap[r * srcEntityCount + i] = outputEntities[r];
                }
            }

            for (var r = 0; r < instanceCount; r++)
            {
                outputRootEntities[r] = entityRemap[r * srcEntityCount];
            }

            for (var i = 0; i < remapChunksCount; i++)
            {
                var remapChunk = remapChunks[i];
                var archetype = ecs->GetArchetype(remapChunk.Chunk);
                var localRemap = entityRemap + remapChunk.InstanceBeginIndex * srcEntityCount;

                EntityRemapUtility.PatchEntitiesForPrefab(archetype->ScalarEntityPatches + 1, archetype->ScalarEntityPatchCount - 1,
                    archetype->BufferEntityPatches, archetype->BufferEntityPatchCount, remapChunk.Chunk.Buffer, remapChunk.IndexInChunk,
                    remapChunk.AllocatedCount, srcEntities, localRemap, srcEntityCount);

#if !UNITY_7000_0_OR_NEWER
                if (archetype->HasManagedEntityRefs)
                {
                    ecs->ManagedChangesTracker.PatchEntitiesForPrefab(archetype, remapChunk.Chunk, remapChunk.IndexInChunk, remapChunk.AllocatedCount,
                        srcEntities, localRemap, srcEntityCount, Allocator.Temp);
                }
#endif
            }
        }

        private static int InstantiateOne(UnityEntities.EntityComponentStore* ecs, Entity srcEntity, Entity* outputEntities, int instanceCount,
            RemapChunk* remapChunks, int remapChunksCount, TypeIndex* typeIndices, int* sharedComponentIndices, int sharedComponentCount)
        {
            var src = ecs->GetEntityInChunk(srcEntity);
            var srcArchetype = ecs->GetArchetype(src.Chunk);
            var dstArchetype = srcArchetype->InstantiateArchetype;

            var srcSharedComponentValues = srcArchetype->Chunks.GetSharedComponentValues(src.Chunk.ListIndex);
            var dstSharedComponentValues = stackalloc int[dstArchetype->NumSharedComponents];

            for (var i = 0; i < dstArchetype->NumSharedComponents; i++)
            {
                var typeIndex = dstArchetype->Types[dstArchetype->FirstSharedComponent + i].TypeIndex;
                var srcIndex = UnityEntities.ChunkDataUtility.GetIndexInTypeArray(srcArchetype, typeIndex) - srcArchetype->FirstSharedComponent;
                dstSharedComponentValues[i] = srcSharedComponentValues[srcIndex];
            }

            for (var i = 0; i < sharedComponentCount; i++)
            {
                var indexInTypeArray = UnityEntities.ChunkDataUtility.GetIndexInTypeArray(dstArchetype, typeIndices[i]);
                if (indexInTypeArray != -1)
                {
                    dstSharedComponentValues[indexInTypeArray - dstArchetype->FirstSharedComponent] = sharedComponentIndices[i];
                }
            }

            var instanceBeginIndex = 0;
            while (instanceBeginIndex != instanceCount)
            {
                var chunk = dstArchetype->GetExistingChunkWithEmptySlots(dstSharedComponentValues);
                if (chunk == UnityEntities.ChunkIndex.Null)
                {
                    chunk = ecs->GetCleanChunk(dstArchetype, dstSharedComponentValues);
                }

                var indexInChunk = chunk.Count;
                var allocateCount = math.min(instanceCount - instanceBeginIndex, dstArchetype->ChunkCapacity - indexInChunk);

                UnityEntities.ChunkDataUtility.AllocateClone(dstArchetype, chunk, outputEntities + instanceBeginIndex, allocateCount, srcEntity);

                remapChunks[remapChunksCount++] = new RemapChunk
                {
                    Chunk = chunk,
                    IndexInChunk = indexInChunk,
                    AllocatedCount = allocateCount,
                    InstanceBeginIndex = instanceBeginIndex
                };

                instanceBeginIndex += allocateCount;
            }

            return remapChunksCount;
        }
    }
}