using System.Runtime.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace NZCore
{
    public static unsafe class ChunkExtensions
    {
        public static void SetChangeVersion<T>(this ArchetypeChunk chunk, ref ComponentTypeHandle<T> handle)
            where T : struct, IComponentData
        {
            ChunkDataUtility.GetIndexInTypeArray(chunk.m_Chunk->Archetype, handle.m_TypeIndex, ref handle.m_LookupCache.IndexInArchetype);
            
            if (handle.m_LookupCache.IndexInArchetype == -1)
                return;

            chunk.m_Chunk->SetChangeVersion(handle.m_LookupCache.IndexInArchetype, handle.GlobalSystemVersion);
        }
        
        public static void SetChangeVersion<T>(this ArchetypeChunk chunk, ref BufferTypeHandle<T> handle)
            where T : unmanaged, IBufferElementData
        {
            var typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(chunk.m_Chunk->Archetype, handle.m_TypeIndex);
            if (typeIndexInArchetype == -1)
                return;

            // This should (=S) be thread safe int writes are atomic in c# <3 tertle
            chunk.m_Chunk->SetChangeVersion(typeIndexInArchetype, handle.GlobalSystemVersion);
        }

        public static BufferAccessor<T> GetBufferAccessor<T>(this ArchetypeChunk chunk, ref BufferTypeHandle<T> bufferComponentTypeHandle, bool bumpVersion = true)
            where T : unmanaged, IBufferElementData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(bufferComponentTypeHandle.m_Safety0);
#endif
            var m_Chunk = chunk.m_Chunk;
            var archetype = m_Chunk->Archetype;
            var typeIndex = bufferComponentTypeHandle.m_TypeIndex;
            var typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);
            if (typeIndexInArchetype == -1)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new BufferAccessor<T>(null, 0, 0, true, bufferComponentTypeHandle.m_Safety0, bufferComponentTypeHandle.m_Safety1, 0);
#else
                return new BufferAccessor<T>(null, 0, 0, 0);
#endif
            }

            int internalCapacity = archetype->BufferCapacities[typeIndexInArchetype];

            byte* ptr = (bufferComponentTypeHandle.IsReadOnly)
                ? ChunkDataUtility.GetComponentDataRO(m_Chunk, 0, typeIndexInArchetype)
                : GetComponentDataRW(m_Chunk, 0, typeIndexInArchetype, bufferComponentTypeHandle.GlobalSystemVersion, bumpVersion);

            var length = chunk.Count;
            int stride = archetype->SizeOfs[typeIndexInArchetype];

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new BufferAccessor<T>(ptr, length, stride, bufferComponentTypeHandle.IsReadOnly, bufferComponentTypeHandle.m_Safety0, bufferComponentTypeHandle.m_Safety1, internalCapacity);
#else
            return new BufferAccessor<T>(ptr, length, stride, internalCapacity);
#endif
        }

        private static byte* GetComponentDataRW(Chunk* chunk, int index, int indexInTypeArray, uint globalSystemVersion, bool bumpVersion = true)
        {
            var offset = chunk->Archetype->Offsets[indexInTypeArray];
            var sizeOf = chunk->Archetype->SizeOfs[indexInTypeArray];

            if (bumpVersion)
            {
                // Write Component to Chunk. ChangeVersion:Yes OrderVersion:No
                chunk->SetChangeVersion(indexInTypeArray, globalSystemVersion);
            }

            return chunk->Buffer + (offset + sizeOf * index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DetermineFastPath(this v128 chunkEnabledMask)
        {
            int edgeCount = 
                math.countbits(chunkEnabledMask.ULong0 ^ (chunkEnabledMask.ULong0 << 1)) +
                math.countbits(chunkEnabledMask.ULong1 ^ (chunkEnabledMask.ULong1 << 1)) - 1;
            
            Debug.Log($"edgeCount {edgeCount}");
            
            return edgeCount <= 4;
        }
    }
}