// <copyright project="Unity.Entities" file="EntityInChunk.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Keep the field order identical to Unity's EntityInChunk, whose fields are internal.
    [StructLayout(LayoutKind.Sequential)]
    public struct EntityInChunk
    {
        public ChunkIndex Chunk;
        public int IndexInChunk;

        internal static EntityInChunk From(UnityEntities.EntityInChunk entityInChunk) =>
            UnsafeUtility.As<UnityEntities.EntityInChunk, EntityInChunk>(ref entityInChunk);
    }
}