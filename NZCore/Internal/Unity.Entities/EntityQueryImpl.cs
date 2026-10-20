// <copyright project="Unity.Entities" file="EntityQueryImpl.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Engine-owned and only reached through pointers. Wraps Unity's query so the layout stays identical; all calls delegate to Unity.
    public unsafe struct EntityQueryImpl
    {
        internal UnityEntities.EntityQueryImpl Value;

        private ref UnityEntities.EntityQueryImpl Self => ref UnsafeUtility.AsRef<UnityEntities.EntityQueryImpl>(UnsafeUtility.AddressOf(ref Value));

        public EntityDataAccess* _Access => (EntityDataAccess*)Value._Access;
        public EntityQueryData* _QueryData => (EntityQueryData*)Value._QueryData;
        public ref EntityQueryFilter _Filter => ref UnsafeUtility.As<UnityEntities.EntityQueryFilter, EntityQueryFilter>(ref Self._Filter);

        public void SyncFilterTypes() => Value.SyncFilterTypes();

        public UnsafeCachedChunkList GetMatchingChunkCache() => UnsafeCachedChunkList.From(Value.GetMatchingChunkCache());

        public void GetSingletonChunkAndEntity(TypeIndex typeIndex, out int outIndexInArchetype, out ChunkIndex outChunk, out int outEntityIndexInChunk)
        {
            Value.GetSingletonChunkAndEntity(typeIndex, out outIndexInArchetype, out var chunk, out outEntityIndexInChunk);
            outChunk = ChunkIndex.From(chunk);
        }

#if UNITY_EDITOR && !UNITY_6000_6_OR_NEWER && !DISABLE_ENTITIES_JOURNALING
        public void RecordSingletonJournalRW(ChunkIndex chunk, TypeIndex typeIndex, EntitiesJournaling.RecordType type) =>
            Value.RecordSingletonJournalRW(chunk.ToUnity(), typeIndex, type);
#endif
    }
}