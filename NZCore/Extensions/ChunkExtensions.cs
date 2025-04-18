﻿// <copyright project="NZCore" file="ChunkExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace NZCore
{
    public static unsafe class ChunkExtensions
    {
        public static void SetChangeVersion<T>(this ArchetypeChunk chunk, ref ComponentTypeHandle<T> handle)
            where T : struct, IComponentData
        {
            var ecs = chunk.m_EntityComponentStore;
            var archetype = ecs->GetArchetype(chunk.m_Chunk);

            if (Hint.Unlikely(archetype != handle.m_LookupCache.Archetype))
                handle.m_LookupCache.Update(archetype, handle.m_TypeIndex);

            var typeIndexInArchetype = handle.m_LookupCache.IndexInArchetype;
            if (typeIndexInArchetype == -1)
                return;

            archetype->Chunks.SetChangeVersion(typeIndexInArchetype, chunk.m_Chunk.ListIndex, handle.GlobalSystemVersion);
        }

        public static void SetChangeVersion<T>(this ArchetypeChunk chunk, ref BufferTypeHandle<T> handle)
            where T : unmanaged, IBufferElementData
        {
            var ecs = chunk.m_EntityComponentStore;
            var archetype = ecs->GetArchetype(chunk.m_Chunk);

            if (Hint.Unlikely(archetype != handle.m_LookupCache.Archetype))
                handle.m_LookupCache.Update(archetype, handle.m_TypeIndex);

            var typeIndexInArchetype = handle.m_LookupCache.IndexInArchetype;
            if (typeIndexInArchetype == -1)
                return;

            archetype->Chunks.SetChangeVersion(typeIndexInArchetype, chunk.m_Chunk.ListIndex, handle.GlobalSystemVersion);
        }

        public static BufferAccessor<T> GetBufferAccessor<T>(this ArchetypeChunk chunk, ref BufferTypeHandle<T> bufferTypeHandle, bool bumpVersion = true)
            where T : unmanaged, IBufferElementData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(bufferTypeHandle.m_Safety0);
#endif
            var ecs = chunk.m_EntityComponentStore;
            var archetype = ecs->GetArchetype(chunk.m_Chunk);
            var typeIndex = bufferTypeHandle.m_TypeIndex;

            if (Hint.Unlikely(bufferTypeHandle.m_LookupCache.Archetype != archetype))
            {
                bufferTypeHandle.m_LookupCache.Update(ecs->GetArchetype(chunk.m_Chunk), typeIndex);
            }

            int typeIndexInArchetype = bufferTypeHandle.m_LookupCache.IndexInArchetype;
            if (typeIndexInArchetype == -1)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new BufferAccessor<T>(null, 0, 0, true, bufferTypeHandle.m_Safety0, bufferTypeHandle.m_Safety1, 0);
#else
                return new BufferAccessor<T>(null, 0, 0, 0);
#endif
            }

            byte* ptr = (bufferTypeHandle.IsReadOnly)
                ? ChunkDataUtility.GetComponentDataRO(chunk.m_Chunk, archetype, 0, typeIndexInArchetype)
                : GetComponentDataRW(chunk.m_Chunk, archetype, 0, typeIndexInArchetype, bufferTypeHandle.GlobalSystemVersion, bumpVersion);

            int internalCapacity = archetype->BufferCapacities[typeIndexInArchetype];
            var length = chunk.Count;
            int stride = archetype->SizeOfs[typeIndexInArchetype];

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new BufferAccessor<T>(ptr, length, stride, bufferTypeHandle.IsReadOnly, bufferTypeHandle.m_Safety0, bufferTypeHandle.m_Safety1, internalCapacity);
#else
            return new BufferAccessor<T>(ptr, length, stride, internalCapacity);
#endif
        }

        private static byte* GetComponentDataRW(ChunkIndex chunk, Archetype* archetype, int index, int indexInTypeArray, uint globalSystemVersion, bool bumpVersion = true)
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
            int edgeCount =
                math.countbits(chunkEnabledMask.ULong0 ^ (chunkEnabledMask.ULong0 << 1)) +
                math.countbits(chunkEnabledMask.ULong1 ^ (chunkEnabledMask.ULong1 << 1)) - 1;

            return edgeCount <= 4;
        }

        public static byte* GetComponentDataRaw(this in ArchetypeChunk chunk, ref DynamicComponentTypeHandle dynamicHandle, bool isReadOnly, out ushort sizeOf)
        {
            var archetype = chunk.Archetype.Archetype;

            var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, dynamicHandle.m_TypeIndex);

            var offset = archetype->Offsets[indexInTypeArray];
            sizeOf = archetype->SizeOfs[indexInTypeArray];

            if (!isReadOnly)
            {
                archetype->Chunks.SetChangeVersion(indexInTypeArray, chunk.m_Chunk.ListIndex, chunk.m_EntityComponentStore->GlobalSystemVersion);
            }

            return chunk.m_Chunk.Buffer + offset;
        }

        public static byte* GetComponentDataRaw(this in ArchetypeChunk chunk, TypeIndex typeIndex, bool isReadOnly, out ushort sizeOf)
        {
            var archetype = chunk.Archetype.Archetype;

            var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);

            var offset = archetype->Offsets[indexInTypeArray];
            sizeOf = archetype->SizeOfs[indexInTypeArray];

            if (!isReadOnly)
            {
                archetype->Chunks.SetChangeVersion(indexInTypeArray, chunk.m_Chunk.ListIndex, chunk.m_EntityComponentStore->GlobalSystemVersion);
            }

            return chunk.m_Chunk.Buffer + offset;
        }

        public static byte* GetComponentDataRaw(this in ArchetypeChunk chunk, TypeIndex typeIndex, int indexInChunk, bool isReadOnly)
        {
            var ecs = chunk.m_EntityComponentStore;

            return isReadOnly
                ? ChunkDataUtility.GetComponentDataWithTypeRO(chunk.m_Chunk, chunk.Archetype.Archetype, indexInChunk, typeIndex)
                : ChunkDataUtility.GetComponentDataWithTypeRW(chunk.m_Chunk, chunk.Archetype.Archetype, indexInChunk, typeIndex, ecs->GlobalSystemVersion);
        }

        public static byte* GetComponentDataRaw(this in ArchetypeChunk chunk, TypeIndex typeIndex, Entity entity, bool isReadOnly)
        {
            var ecs = chunk.m_EntityComponentStore;
            var entityInChunk = ecs->GetEntityInChunk(entity);

            return isReadOnly
                ? ChunkDataUtility.GetComponentDataWithTypeRO(chunk.m_Chunk, chunk.Archetype.Archetype, entityInChunk.IndexInChunk, typeIndex)
                : ChunkDataUtility.GetComponentDataWithTypeRW(chunk.m_Chunk, chunk.Archetype.Archetype, entityInChunk.IndexInChunk, typeIndex, ecs->GlobalSystemVersion);
        }
        
        public static byte* GetSharedComponentDataRaw(this in ArchetypeChunk chunk, TypeIndex typeIndex, Entity entity)
        {
            var ecs = chunk.m_EntityComponentStore;
            var sharedComponentIndex = ecs->GetSharedComponentDataIndex(entity, typeIndex);
            return (byte*) ecs->GetSharedComponentDataAddr_Unmanaged(sharedComponentIndex, typeIndex);
        }

        public static byte* GetSharedComponentDataRaw(this in ArchetypeChunk chunk, TypeIndex typeIndex)
        {
            var ecs = chunk.m_EntityComponentStore;
            var sharedComponentIndex = GetSharedComponentDataIndex(chunk, typeIndex);
            return (byte*) ecs->GetSharedComponentDataAddr_Unmanaged(sharedComponentIndex, typeIndex);
        }

        public static int GetSharedComponentDataIndex(this in ArchetypeChunk chunk, TypeIndex typeIndex)
        {
            var ecs = chunk.m_EntityComponentStore;
            var archetype = ecs->GetArchetype(chunk.m_Chunk);
            var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);
            
            var sharedComponentValueArray = archetype->Chunks.GetSharedComponentValues(chunk.m_Chunk.ListIndex);
            var sharedComponentOffset = indexInTypeArray - archetype->FirstSharedComponent;
            return sharedComponentValueArray[sharedComponentOffset];
        }
        
        public static ulong* GetRequiredEnabledBitsPtrRW<T>(this in ArchetypeChunk archetypeChunk, ref ComponentTypeHandle<T> typeHandle, out int* ptrChunkDisabledCount)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(typeHandle.m_Safety);
#endif
            var archetype = archetypeChunk.m_EntityComponentStore->GetArchetype(archetypeChunk.m_Chunk);

            if (Hint.Unlikely(typeHandle.m_LookupCache.Archetype != archetype))
            {
                typeHandle.m_LookupCache.Update(archetype, typeHandle.m_TypeIndex);
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Hint.Unlikely(typeHandle.IsReadOnly))
            {
                throw new InvalidOperationException("Provided ComponentTypeHandle is read-only; can't get a read/write pointer to component data");
            }

            // Must check this after computing the pointer, to make sure the cache is up to date
            if (Hint.Unlikely(typeHandle.m_LookupCache.IndexInArchetype == -1))
            {
                var typeName = typeHandle.m_TypeIndex.ToFixedString();
                throw new ArgumentException($"Required component {typeName} not found in archetype.");
            }
#endif

            var ptr = ChunkDataUtility.GetEnabledRefRW(
                archetypeChunk.m_Chunk,
                archetypeChunk.Archetype.Archetype,
                typeHandle.m_LookupCache.IndexInArchetype,
                typeHandle.GlobalSystemVersion,
                out ptrChunkDisabledCount).Ptr;

            return ptr;
        }

        public static ref v128 GetRequiredEnabledBitsRW<T>(this in ArchetypeChunk archetypeChunk, ref ComponentTypeHandle<T> typeHandle, out int* ptrChunkDisabledCount)
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
            AtomicSafetyHandle.CheckReadAndThrow(bufferTypeHandle.m_Safety0);
#endif
            var ecs = chunk.m_EntityComponentStore;
            //var chunkPtr = chunk.m_Chunk;
            var archetype = ecs->GetArchetype(chunk.m_Chunk);
            var typeIndex = bufferTypeHandle.m_TypeIndex;
            if (Hint.Unlikely(bufferTypeHandle.m_LookupCache.Archetype != archetype))
            {
                bufferTypeHandle.m_LookupCache.Update(archetype, typeIndex);
            }

            byte* ptr = (bufferTypeHandle.IsReadOnly)
                ? ChunkDataUtility.GetComponentDataWithTypeRO(chunk.m_Chunk, archetype, 0, typeIndex, ref bufferTypeHandle.m_LookupCache)
                : ChunkDataUtility.GetComponentDataWithTypeRW(chunk.m_Chunk, archetype, 0, typeIndex, bufferTypeHandle.GlobalSystemVersion, ref bufferTypeHandle.m_LookupCache);

            int internalCapacity = archetype->BufferCapacities[bufferTypeHandle.m_LookupCache.IndexInArchetype];
            var length = chunk.Count;
            int stride = bufferTypeHandle.m_LookupCache.ComponentSizeOf;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new BufferAccessor<T>(ptr, length, stride, bufferTypeHandle.IsReadOnly, bufferTypeHandle.m_Safety0, bufferTypeHandle.m_Safety1, internalCapacity);
#else
            return new BufferAccessor<T>(ptr, length, stride, internalCapacity);
#endif
        }
        
        public static void CopyEnableMaskFrom<TD, TS>(this ArchetypeChunk archetypeChunk, ref ComponentTypeHandle<TD> destination, ref ComponentTypeHandle<TS> source)
            where TD : unmanaged, IComponentData, IEnableableComponent
            where TS : unmanaged, IComponentData, IEnableableComponent
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(destination.m_Safety);
            AtomicSafetyHandle.CheckReadAndThrow(source.m_Safety);
#endif
            var archetype = archetypeChunk.m_EntityComponentStore->GetArchetype(archetypeChunk.m_Chunk);

            if (Hint.Unlikely(destination.m_LookupCache.Archetype != archetype))
            {
                destination.m_LookupCache.Update(archetype, destination.m_TypeIndex);
            }

            if (Hint.Unlikely(source.m_LookupCache.Archetype != archetype))
            {
                source.m_LookupCache.Update(archetype, source.m_TypeIndex);
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Hint.Unlikely(destination.m_LookupCache.IndexInArchetype == -1))
            {
                throw new InvalidOperationException();
            }

            if (Hint.Unlikely(source.m_LookupCache.IndexInArchetype == -1))
            {
                throw new InvalidOperationException();
            }
#endif
            var dst = ChunkDataUtility.GetEnabledRefRW(
                archetypeChunk.m_Chunk, archetypeChunk.Archetype.Archetype, destination.m_LookupCache.IndexInArchetype, destination.GlobalSystemVersion,
                out var dstPtrChunkDisabledCount).Ptr;

            var src = ChunkDataUtility.GetEnabledRefRO(archetypeChunk.m_Chunk, archetypeChunk.Archetype.Archetype, source.m_LookupCache.IndexInArchetype).Ptr;

            var chunks = archetype->Chunks;
            int memoryOrderIndexInArchetype = archetype->TypeIndexInArchetypeToMemoryOrderIndex[source.m_LookupCache.IndexInArchetype];
            var srcPtrChunkDisabledCount = chunks.GetPointerToChunkDisabledCountForType(memoryOrderIndexInArchetype, archetypeChunk.m_Chunk.ListIndex);

            dst[0] = src[0];
            dst[1] = src[1];
            *dstPtrChunkDisabledCount = *srcPtrChunkDisabledCount;
        }
        
        public static ref T GetChunkComponentDataRW<T>(this ArchetypeChunk archetypeChunk, ref ComponentTypeHandle<T> typeHandle)
            where T : unmanaged, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(typeHandle.m_Safety);
#endif
            var metaChunkEntity = archetypeChunk.m_Chunk.MetaChunkEntity;
            archetypeChunk.m_EntityComponentStore->AssertEntityHasComponent(metaChunkEntity, typeHandle.m_TypeIndex);
            var ptr = archetypeChunk.m_EntityComponentStore->GetComponentDataWithTypeRW(metaChunkEntity, typeHandle.m_TypeIndex, typeHandle.GlobalSystemVersion);
            return ref UnsafeUtility.AsRef<T>(ptr);
        }
    }
}