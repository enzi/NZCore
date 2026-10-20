// <copyright project="Unity.Entities" file="ChunkIterationUtility.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Direct calls only: Unity owns the accessor construction and its safety setup.
    public static unsafe class ChunkIterationUtility
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public static BufferAccessor<T> GetChunkBufferAccessor<T>(Archetype* archetype, ChunkIndex chunk, bool isWriting, int typeIndexInArchetype,
            uint systemVersion, AtomicSafetyHandle safety0, AtomicSafetyHandle safety1)
            where T : unmanaged, IBufferElementData =>
            UnityEntities.ChunkIterationUtility.GetChunkBufferAccessor<T>((UnityEntities.Archetype*)archetype, chunk.ToUnity(), isWriting,
                typeIndexInArchetype, systemVersion, safety0, safety1);
#else
        public static BufferAccessor<T> GetChunkBufferAccessor<T>(Archetype* archetype, ChunkIndex chunk, bool isWriting, int typeIndexInArchetype,
            uint systemVersion)
            where T : unmanaged, IBufferElementData =>
            UnityEntities.ChunkIterationUtility.GetChunkBufferAccessor<T>((UnityEntities.Archetype*)archetype, chunk.ToUnity(), isWriting,
                typeIndexInArchetype, systemVersion);
#endif
    }
}