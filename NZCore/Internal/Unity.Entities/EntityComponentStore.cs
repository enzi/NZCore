// <copyright project="Unity.Entities" file="EntityComponentStore.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Diagnostics;
using Unity.Entities;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Engine-owned and only reached through pointers. Wraps Unity's store so the layout stays identical; all calls delegate to Unity.
    public unsafe struct EntityComponentStore
    {
        public struct ArchetypeChanges
        {
            internal UnityEntities.EntityComponentStore.ArchetypeChanges Value;
        }

        internal UnityEntities.EntityComponentStore Value;

        public uint GlobalSystemVersion => Value.GlobalSystemVersion;

#if (!UNITY_7000_0_OR_NEWER && UNITY_INCLUDE_INSTRUMENTATION || (!UNITY_6000_6_OR_NEWER && UNITY_EDITOR)) && !DISABLE_ENTITIES_JOURNALING
        public byte m_RecordToJournal => Value.m_RecordToJournal;
#endif

        public void IncrementGlobalSystemVersion(in SystemHandle handle = default) => Value.IncrementGlobalSystemVersion(in handle);

        public bool Exists(Entity entity) => Value.Exists(entity);

        public ChunkIndex GetChunk(Entity entity) => ChunkIndex.From(Value.GetChunk(entity));

        public Archetype* GetArchetype(ChunkIndex chunk) => (Archetype*)Value.GetArchetype(chunk.ToUnity());

        public EntityInChunk GetEntityInChunk(Entity entity) => EntityInChunk.From(Value.GetEntityInChunk(entity));

        public bool HasComponent(Entity entity, TypeIndex type, out bool entityExists) => Value.HasComponent(entity, type, out entityExists);

        public bool HasComponent(Entity entity, TypeIndex type, ref LookupCache cache, out bool entityExists) =>
            Value.HasComponent(entity, type, ref LookupCache.AsUnity(ref cache), out entityExists);

        [Conditional("UNITY_DOTS_DEBUG")]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void AssertEntityHasComponent(Entity entity, TypeIndex componentTypeIndex) => Value.AssertEntityHasComponent(entity, componentTypeIndex);

        [Conditional("UNITY_DOTS_DEBUG")]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void AssertEntityHasComponent(Entity entity, TypeIndex componentTypeIndex, ref LookupCache typeLookupCache) =>
            Value.AssertEntityHasComponent(entity, componentTypeIndex, ref LookupCache.AsUnity(ref typeLookupCache));

        public byte* GetComponentDataWithTypeRO(Entity entity, TypeIndex typeIndex) => Value.GetComponentDataWithTypeRO(entity, typeIndex);

        public byte* GetComponentDataWithTypeRO(Entity entity, TypeIndex typeIndex, ref LookupCache cache) =>
            Value.GetComponentDataWithTypeRO(entity, typeIndex, ref LookupCache.AsUnity(ref cache));

        public byte* GetComponentDataWithTypeRW(Entity entity, TypeIndex typeIndex, uint globalVersion) =>
            Value.GetComponentDataWithTypeRW(entity, typeIndex, globalVersion);

        public byte* GetComponentDataWithTypeRW(Entity entity, TypeIndex typeIndex, uint globalVersion, ref LookupCache cache) =>
            Value.GetComponentDataWithTypeRW(entity, typeIndex, globalVersion, ref LookupCache.AsUnity(ref cache));

        public byte* GetOptionalComponentDataWithTypeRO(Entity entity, TypeIndex typeIndex, ref LookupCache cache) =>
            Value.GetOptionalComponentDataWithTypeRO(entity, typeIndex, ref LookupCache.AsUnity(ref cache));

        public byte* GetOptionalComponentDataWithTypeRW(Entity entity, TypeIndex typeIndex, uint globalVersion, ref LookupCache cache) =>
            Value.GetOptionalComponentDataWithTypeRW(entity, typeIndex, globalVersion, ref LookupCache.AsUnity(ref cache));

        public ulong* GetEnabledRawRO(Entity entity, TypeIndex typeIndex, ref LookupCache typeLookupCache, out int indexInBitField,
            out int* ptrChunkDisabledCount) =>
            Value.GetEnabledRawRO(entity, typeIndex, ref LookupCache.AsUnity(ref typeLookupCache), out indexInBitField, out ptrChunkDisabledCount);

        public ulong* GetEnabledRawRW(Entity entity, TypeIndex typeIndex, ref LookupCache typeLookupCache, uint globalSystemVersion,
            out int indexInBitField, out int* ptrChunkDisabledCount) =>
            Value.GetEnabledRawRW(entity, typeIndex, ref LookupCache.AsUnity(ref typeLookupCache), globalSystemVersion, out indexInBitField,
                out ptrChunkDisabledCount);

        public int GetSharedComponentDataIndex(Entity entity, TypeIndex typeIndex) => Value.GetSharedComponentDataIndex(entity, typeIndex);

        public void* GetSharedComponentDataAddr_Unmanaged(int sharedComponentIndex, TypeIndex typeIndex) =>
            Value.GetSharedComponentDataAddr_Unmanaged(sharedComponentIndex, typeIndex);
    }
}