// <copyright project="Unity.Entities" file="Archetype.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections.LowLevel.Unsafe;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Engine-owned and only reached through pointers. Wraps Unity's archetype so the layout stays identical; all reads delegate to Unity.
    public unsafe struct Archetype
    {
        internal UnityEntities.Archetype Value;

        private ref UnityEntities.Archetype Self => ref UnsafeUtility.AsRef<UnityEntities.Archetype>(UnsafeUtility.AddressOf(ref Value));

        public ref ArchetypeChunkData Chunks => ref UnsafeUtility.As<UnityEntities.ArchetypeChunkData, ArchetypeChunkData>(ref Self.Chunks);

        public int* TypeIndexInArchetypeToMemoryOrderIndex => Value.TypeIndexInArchetypeToMemoryOrderIndex;
        public int* Offsets => Value.Offsets;
        public ushort* SizeOfs => Value.SizeOfs;
        public int* BufferCapacities => Value.BufferCapacities;
        public short FirstSharedComponent => Value.FirstSharedComponent;
    }
}