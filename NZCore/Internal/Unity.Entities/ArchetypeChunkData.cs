// <copyright project="Unity.Entities" file="ArchetypeChunkData.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Lives inside Archetype. Wraps Unity's per-chunk data so the layout stays identical; all calls delegate to Unity.
    public unsafe struct ArchetypeChunkData
    {
        internal UnityEntities.ArchetypeChunkData Value;

        public uint GetChangeVersion(int indexInArchetype, int chunkIndex) => Value.GetChangeVersion(indexInArchetype, chunkIndex);

        public void SetChangeVersion(int indexInArchetype, int chunkIndex, uint version) => Value.SetChangeVersion(indexInArchetype, chunkIndex, version);

        public SharedComponentValues GetSharedComponentValues(int chunkIndex) => SharedComponentValues.From(Value.GetSharedComponentValues(chunkIndex));

        public int* GetPointerToChunkDisabledCountForType(int typeMemoryOrderIndexInArchetype, int chunkIndex) =>
            Value.GetPointerToChunkDisabledCountForType(typeMemoryOrderIndexInArchetype, chunkIndex);
    }
}