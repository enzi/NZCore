// <copyright project="NZCore" file="ChunkExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using NZCore.Internal;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Archetype = NZCore.Internal.Archetype;
using ChunkDataUtility = NZCore.Internal.ChunkDataUtility;
using ChunkIndex = NZCore.Internal.ChunkIndex;

namespace NZCore
{
    public static unsafe class ChunkExtensions
    {
        public static void SetChangeVersion<T>(this ArchetypeChunk chunk, ref ComponentTypeHandle<T> handle)
            where T : struct, IComponentData
        {
            var ecs = chunk.GetEntityComponentStore();
            var archetype = ecs->GetArchetype(chunk.GetChunkIndex());

            if (Hint.Unlikely(archetype != handle.GetLookupCache().Archetype))
            {
                handle.GetLookupCache().Update(archetype, handle.GetTypeIndex());
            }

            var typeIndexInArchetype = handle.GetLookupCache().IndexInArchetype;
            if (typeIndexInArchetype == -1)
            {
                return;
            }

            archetype->Chunks.SetChangeVersion(typeIndexInArchetype, chunk.GetChunkIndex().ListIndex, handle.GlobalSystemVersion);
        }

        public static void SetChangeVersion<T>(this ArchetypeChunk chunk, ref BufferTypeHandle<T> handle)
            where T : unmanaged, IBufferElementData
        {
            var ecs = chunk.GetEntityComponentStore();
            var archetype = ecs->GetArchetype(chunk.GetChunkIndex());

            if (Hint.Unlikely(archetype != handle.GetLookupCache().Archetype))
            {
                handle.GetLookupCache().Update(archetype, handle.GetTypeIndex());
            }

            var typeIndexInArchetype = handle.GetLookupCache().IndexInArchetype;
            if (typeIndexInArchetype == -1)
            {
                return;
            }

            archetype->Chunks.SetChangeVersion(typeIndexInArchetype, chunk.GetChunkIndex().ListIndex, handle.GlobalSystemVersion);
        }

        public static BufferAccessor<T> GetBufferAccessor<T>(this ArchetypeChunk chunk, ref BufferTypeHandle<T> bufferTypeHandle, bool bumpVersion = true)
            where T : unmanaged, IBufferElementData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(bufferTypeHandle.GetSafety0());
#endif
            var ecs = chunk.GetEntityComponentStore();
            var archetype = ecs->GetArchetype(chunk.GetChunkIndex());
            var typeIndex = bufferTypeHandle.GetTypeIndex();

            if (Hint.Unlikely(bufferTypeHandle.GetLookupCache().Archetype != archetype))
            {
                bufferTypeHandle.GetLookupCache().Update(ecs->GetArchetype(chunk.GetChunkIndex()), typeIndex);
            }

            int typeIndexInArchetype = bufferTypeHandle.GetLookupCache().IndexInArchetype;
            if (typeIndexInArchetype == -1)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return EntityInternals.CreateBufferAccessor<T>(null, 0, 0, true, bufferTypeHandle.GetSafety0(), bufferTypeHandle.GetSafety1(), 0);
#else
                return EntityInternals.CreateBufferAccessor<T>(null, 0, 0, 0);
#endif
            }

            var ptr = bufferTypeHandle.IsReadOnly
                ? ChunkDataUtility.GetComponentDataRO(chunk.GetChunkIndex(), archetype, 0, typeIndexInArchetype)
                : GetComponentDataRW(chunk.GetChunkIndex(), archetype, 0, typeIndexInArchetype, bufferTypeHandle.GlobalSystemVersion, bumpVersion);

            var internalCapacity = archetype->BufferCapacities[typeIndexInArchetype];
            var length = chunk.Count;
            int stride = archetype->SizeOfs[typeIndexInArchetype];

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return EntityInternals.CreateBufferAccessor<T>(ptr, length, stride, bufferTypeHandle.IsReadOnly, bufferTypeHandle.GetSafety0(),
                bufferTypeHandle.GetSafety1(),
                internalCapacity);
#else
            return EntityInternals.CreateBufferAccessor<T>(ptr, length, stride, internalCapacity);
#endif
        }

        private static byte* GetComponentDataRW(ChunkIndex chunk, Archetype* archetype, int index, int indexInTypeArray, uint globalSystemVersion,
            bool bumpVersion = true)
        {
            var offset = archetype->Offsets[indexInTypeArray];
            var sizeOf = archetype->SizeOfs[indexInTypeArray];

            if (bumpVersion)
            {
                // Write Component to Chunk. ChangeVersion:Yes OrderVersion:No
                archetype->Chunks.SetChangeVersion(indexInTypeArray, chunk.ListIndex, globalSystemVersion);
            }

            return chunk.Buffer + (offset + sizeOf * index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DetermineFastPath(this v128 chunkEnabledMask)
        {
            var edgeCount =
                math.countbits(chunkEnabledMask.ULong0 ^ (chunkEnabledMask.ULong0 << 1)) +
                math.countbits(chunkEnabledMask.ULong1 ^ (chunkEnabledMask.ULong1 << 1)) - 1;

            return edgeCount <= 4;
        }

        public static byte* GetComponentDataRaw(this in ArchetypeChunk chunk, ref DynamicComponentTypeHandle dynamicHandle, bool isReadOnly, out ushort sizeOf)
        {
            var archetype = chunk.Archetype.GetArchetype();

            var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, dynamicHandle.GetTypeIndex());

            var offset = archetype->Offsets[indexInTypeArray];
            sizeOf = archetype->SizeOfs[indexInTypeArray];

            if (!isReadOnly)
            {
                archetype->Chunks.SetChangeVersion(indexInTypeArray, chunk.GetChunkIndex().ListIndex, chunk.GetEntityComponentStore()->GlobalSystemVersion);
            }

            return chunk.GetChunkIndex().Buffer + offset;
        }

        public static byte* GetComponentDataRaw(this in ArchetypeChunk chunk, TypeIndex typeIndex, bool isReadOnly, out ushort sizeOf)
        {
            var archetype = chunk.Archetype.GetArchetype();

            var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);

            var offset = archetype->Offsets[indexInTypeArray];
            sizeOf = archetype->SizeOfs[indexInTypeArray];

            if (!isReadOnly)
            {
                archetype->Chunks.SetChangeVersion(indexInTypeArray, chunk.GetChunkIndex().ListIndex, chunk.GetEntityComponentStore()->GlobalSystemVersion);
            }

            return chunk.GetChunkIndex().Buffer + offset;
        }

        public static byte* GetComponentDataRaw(this in ArchetypeChunk chunk, TypeIndex typeIndex, int indexInChunk, bool isReadOnly)
        {
            var ecs = chunk.GetEntityComponentStore();

            return isReadOnly
                ? ChunkDataUtility.GetComponentDataWithTypeRO(chunk.GetChunkIndex(), chunk.Archetype.GetArchetype(), indexInChunk, typeIndex)
                : ChunkDataUtility.GetComponentDataWithTypeRW(chunk.GetChunkIndex(), chunk.Archetype.GetArchetype(), indexInChunk, typeIndex,
                    ecs->GlobalSystemVersion);
        }

        public static byte* GetComponentDataRaw(this in ArchetypeChunk chunk, TypeIndex typeIndex, Entity entity, bool isReadOnly)
        {
            var ecs = chunk.GetEntityComponentStore();
            var entityInChunk = ecs->GetEntityInChunk(entity);

            return isReadOnly
                ? ChunkDataUtility.GetComponentDataWithTypeRO(chunk.GetChunkIndex(), chunk.Archetype.GetArchetype(), entityInChunk.IndexInChunk, typeIndex)
                : ChunkDataUtility.GetComponentDataWithTypeRW(chunk.GetChunkIndex(), chunk.Archetype.GetArchetype(), entityInChunk.IndexInChunk, typeIndex,
                    ecs->GlobalSystemVersion);
        }

        public static byte* GetSharedComponentDataRaw(this in ArchetypeChunk chunk, TypeIndex typeIndex, Entity entity)
        {
            var ecs = chunk.GetEntityComponentStore();
            var sharedComponentIndex = ecs->GetSharedComponentDataIndex(entity, typeIndex);
            return (byte*)ecs->GetSharedComponentDataAddr_Unmanaged(sharedComponentIndex, typeIndex);
        }

        public static byte* GetSharedComponentDataRaw(this in ArchetypeChunk chunk, TypeIndex typeIndex)
        {
            var ecs = chunk.GetEntityComponentStore();
            var sharedComponentIndex = GetSharedComponentDataIndex(chunk, typeIndex);
            return (byte*)ecs->GetSharedComponentDataAddr_Unmanaged(sharedComponentIndex, typeIndex);
        }

        public static int GetSharedComponentDataIndex(this in ArchetypeChunk chunk, TypeIndex typeIndex)
        {
            var ecs = chunk.GetEntityComponentStore();
            var archetype = ecs->GetArchetype(chunk.GetChunkIndex());
            var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);

            var sharedComponentValueArray = archetype->Chunks.GetSharedComponentValues(chunk.GetChunkIndex().ListIndex);
            var sharedComponentOffset = indexInTypeArray - archetype->FirstSharedComponent;
            return sharedComponentValueArray[sharedComponentOffset];
        }

        public static ulong* GetRequiredEnabledBitsPtrRW<T>(this in ArchetypeChunk archetypeChunk, ref ComponentTypeHandle<T> typeHandle,
            out int* ptrChunkDisabledCount)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(typeHandle.GetSafety());
#endif
            var archetype = archetypeChunk.GetEntityComponentStore()->GetArchetype(archetypeChunk.GetChunkIndex());

            if (Hint.Unlikely(typeHandle.GetLookupCache().Archetype != archetype))
            {
                typeHandle.GetLookupCache().Update(archetype, typeHandle.GetTypeIndex());
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Hint.Unlikely(typeHandle.IsReadOnly))
            {
                throw new InvalidOperationException("Provided ComponentTypeHandle is read-only; can't get a read/write pointer to component data");
            }

            // Must check this after computing the pointer, to make sure the cache is up to date
            if (Hint.Unlikely(typeHandle.GetLookupCache().IndexInArchetype == -1))
            {
                var typeName = typeHandle.GetTypeIndex().ToFixedString();
                throw new ArgumentException($"Required component {typeName} not found in archetype.");
            }
#endif

            var ptr = ChunkDataUtility.GetEnabledRefRW(
                archetypeChunk.GetChunkIndex(),
                archetypeChunk.Archetype.GetArchetype(),
                typeHandle.GetLookupCache().IndexInArchetype,
                typeHandle.GlobalSystemVersion,
                out ptrChunkDisabledCount).Ptr;

            return ptr;
        }

        public static ref v128 GetRequiredEnabledBitsRW<T>(this in ArchetypeChunk archetypeChunk, ref ComponentTypeHandle<T> typeHandle,
            out int* ptrChunkDisabledCount)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            var ptr = GetRequiredEnabledBitsPtrRW(archetypeChunk, ref typeHandle, out ptrChunkDisabledCount);
            return ref UnsafeUtility.AsRef<v128>(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateChunkDisabledCount(this in ArchetypeChunk archetypeChunk, int* ptrChunkDisabledCount, ulong* enabledMaskPtr)
        {
            ref var bits = ref UnsafeUtility.AsRef<v128>(enabledMaskPtr);
            *ptrChunkDisabledCount = archetypeChunk.Count - math.countbits(bits.ULong0) - math.countbits(bits.ULong1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateChunkDisabledCount(this in ArchetypeChunk archetypeChunk, int* ptrChunkDisabledCount, in v128 bits)
        {
            *ptrChunkDisabledCount = archetypeChunk.Count - math.countbits(bits.ULong0) - math.countbits(bits.ULong1);
        }

        public static BufferAccessor<T> GetRequiredBufferAccessor<T>(this ArchetypeChunk chunk, ref BufferTypeHandle<T> bufferTypeHandle)
            where T : unmanaged, IBufferElementData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(bufferTypeHandle.GetSafety0());
#endif
            var ecs = chunk.GetEntityComponentStore();
            //var chunkPtr = chunk.GetChunkIndex();
            var archetype = ecs->GetArchetype(chunk.GetChunkIndex());
            var typeIndex = bufferTypeHandle.GetTypeIndex();
            if (Hint.Unlikely(bufferTypeHandle.GetLookupCache().Archetype != archetype))
            {
                bufferTypeHandle.GetLookupCache().Update(archetype, typeIndex);
            }

            var ptr = bufferTypeHandle.IsReadOnly
                ? ChunkDataUtility.GetComponentDataWithTypeRO(chunk.GetChunkIndex(), archetype, 0, typeIndex, ref bufferTypeHandle.GetLookupCache())
                : ChunkDataUtility.GetComponentDataWithTypeRW(chunk.GetChunkIndex(), archetype, 0, typeIndex, bufferTypeHandle.GlobalSystemVersion,
                    ref bufferTypeHandle.GetLookupCache());

            var internalCapacity = archetype->BufferCapacities[bufferTypeHandle.GetLookupCache().IndexInArchetype];
            var length = chunk.Count;
            int stride = bufferTypeHandle.GetLookupCache().ComponentSizeOf;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return EntityInternals.CreateBufferAccessor<T>(ptr, length, stride, bufferTypeHandle.IsReadOnly, bufferTypeHandle.GetSafety0(),
                bufferTypeHandle.GetSafety1(),
                internalCapacity);
#else
            return EntityInternals.CreateBufferAccessor<T>(ptr, length, stride, internalCapacity);
#endif
        }

        public static void CopyEnableMaskFrom<TD, TS>(this ArchetypeChunk archetypeChunk, ref ComponentTypeHandle<TD> destination,
            ref ComponentTypeHandle<TS> source)
            where TD : unmanaged, IComponentData, IEnableableComponent
            where TS : unmanaged, IComponentData, IEnableableComponent
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(destination.GetSafety());
            AtomicSafetyHandle.CheckReadAndThrow(source.GetSafety());
#endif
            var archetype = archetypeChunk.GetEntityComponentStore()->GetArchetype(archetypeChunk.GetChunkIndex());

            if (Hint.Unlikely(destination.GetLookupCache().Archetype != archetype))
            {
                destination.GetLookupCache().Update(archetype, destination.GetTypeIndex());
            }

            if (Hint.Unlikely(source.GetLookupCache().Archetype != archetype))
            {
                source.GetLookupCache().Update(archetype, source.GetTypeIndex());
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Hint.Unlikely(destination.GetLookupCache().IndexInArchetype == -1))
            {
                throw new InvalidOperationException();
            }

            if (Hint.Unlikely(source.GetLookupCache().IndexInArchetype == -1))
            {
                throw new InvalidOperationException();
            }
#endif
            var dst = ChunkDataUtility.GetEnabledRefRW(
                archetypeChunk.GetChunkIndex(), archetypeChunk.Archetype.GetArchetype(), destination.GetLookupCache().IndexInArchetype,
                destination.GlobalSystemVersion,
                out var dstPtrChunkDisabledCount).Ptr;

            var src = ChunkDataUtility.GetEnabledRefRO(archetypeChunk.GetChunkIndex(), archetypeChunk.Archetype.GetArchetype(),
                source.GetLookupCache().IndexInArchetype).Ptr;

            ref var chunks = ref archetype->Chunks;
            var memoryOrderIndexInArchetype = archetype->TypeIndexInArchetypeToMemoryOrderIndex[source.GetLookupCache().IndexInArchetype];
            var srcPtrChunkDisabledCount = chunks.GetPointerToChunkDisabledCountForType(memoryOrderIndexInArchetype, archetypeChunk.GetChunkIndex().ListIndex);

            dst[0] = src[0];
            dst[1] = src[1];
            *dstPtrChunkDisabledCount = *srcPtrChunkDisabledCount;
        }

        public static ref T GetChunkComponentDataRW<T>(this ArchetypeChunk archetypeChunk, ref ComponentTypeHandle<T> typeHandle)
            where T : unmanaged, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(typeHandle.GetSafety());
#endif
            var metaChunkEntity = archetypeChunk.GetChunkIndex().MetaChunkEntity;
            archetypeChunk.GetEntityComponentStore()->AssertEntityHasComponent(metaChunkEntity, typeHandle.GetTypeIndex());
            var ptr = archetypeChunk.GetEntityComponentStore()->GetComponentDataWithTypeRW(metaChunkEntity, typeHandle.GetTypeIndex(),
                typeHandle.GlobalSystemVersion);
            return ref UnsafeUtility.AsRef<T>(ptr);
        }
    }
}