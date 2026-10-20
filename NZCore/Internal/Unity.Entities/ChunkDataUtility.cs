// <copyright project="Unity.Entities" file="ChunkDataUtility.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Direct calls only: Unity owns the chunk memory layout and change-version bookkeeping.
    public static unsafe class ChunkDataUtility
    {
        public static int GetIndexInTypeArray(Archetype* archetype, TypeIndex typeIndex) =>
            UnityEntities.ChunkDataUtility.GetIndexInTypeArray((UnityEntities.Archetype*)archetype, typeIndex);

        public static byte* GetComponentDataRO(ChunkIndex chunk, Archetype* archetype, int baseEntityIndex, int indexInTypeArray) =>
            UnityEntities.ChunkDataUtility.GetComponentDataRO(chunk.ToUnity(), (UnityEntities.Archetype*)archetype, baseEntityIndex, indexInTypeArray);

        public static byte* GetComponentDataWithTypeRO(ChunkIndex chunk, Archetype* archetype, int baseEntityIndex, TypeIndex typeIndex) =>
            UnityEntities.ChunkDataUtility.GetComponentDataWithTypeRO(chunk.ToUnity(), (UnityEntities.Archetype*)archetype, baseEntityIndex, typeIndex);

        public static byte* GetComponentDataWithTypeRO(ChunkIndex chunk, Archetype* archetype, int baseEntityIndex, TypeIndex typeIndex,
            ref LookupCache lookupCache) =>
            UnityEntities.ChunkDataUtility.GetComponentDataWithTypeRO(chunk.ToUnity(), (UnityEntities.Archetype*)archetype, baseEntityIndex, typeIndex,
                ref LookupCache.AsUnity(ref lookupCache));

        public static byte* GetComponentDataWithTypeRW(ChunkIndex chunk, Archetype* archetype, int baseEntityIndex, TypeIndex typeIndex,
            uint globalSystemVersion) =>
            UnityEntities.ChunkDataUtility.GetComponentDataWithTypeRW(chunk.ToUnity(), (UnityEntities.Archetype*)archetype, baseEntityIndex, typeIndex,
                globalSystemVersion);

        public static byte* GetComponentDataWithTypeRW(ChunkIndex chunk, Archetype* archetype, int baseEntityIndex, TypeIndex typeIndex,
            uint globalSystemVersion, ref LookupCache lookupCache) =>
            UnityEntities.ChunkDataUtility.GetComponentDataWithTypeRW(chunk.ToUnity(), (UnityEntities.Archetype*)archetype, baseEntityIndex, typeIndex,
                globalSystemVersion, ref LookupCache.AsUnity(ref lookupCache));

        public static byte* GetOptionalComponentDataWithTypeRO(ChunkIndex chunk, Archetype* archetype, int baseEntityIndex, TypeIndex typeIndex,
            ref LookupCache lookupCache) =>
            UnityEntities.ChunkDataUtility.GetOptionalComponentDataWithTypeRO(chunk.ToUnity(), (UnityEntities.Archetype*)archetype, baseEntityIndex,
                typeIndex, ref LookupCache.AsUnity(ref lookupCache));

        public static byte* GetOptionalComponentDataWithTypeRW(ChunkIndex chunk, Archetype* archetype, int baseEntityIndex, TypeIndex typeIndex,
            uint globalSystemVersion, ref LookupCache lookupCache) =>
            UnityEntities.ChunkDataUtility.GetOptionalComponentDataWithTypeRW(chunk.ToUnity(), (UnityEntities.Archetype*)archetype, baseEntityIndex,
                typeIndex, globalSystemVersion, ref LookupCache.AsUnity(ref lookupCache));

        public static UnsafeBitArray GetEnabledRefRO(ChunkIndex chunk, Archetype* archetype, int indexInTypeArray) =>
            UnityEntities.ChunkDataUtility.GetEnabledRefRO(chunk.ToUnity(), (UnityEntities.Archetype*)archetype, indexInTypeArray);

        public static UnsafeBitArray GetEnabledRefRW(ChunkIndex chunk, Archetype* archetype, int indexInTypeArray, uint globalSystemVersion,
            out int* ptrChunkDisabledCount) =>
            UnityEntities.ChunkDataUtility.GetEnabledRefRW(chunk.ToUnity(), (UnityEntities.Archetype*)archetype, indexInTypeArray, globalSystemVersion,
                out ptrChunkDisabledCount);
    }
}