// <copyright project="Unity.Entities" file="ChunkIndex.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Keep the layout identical to Unity's chunk index; all chunk metadata is read through Unity.
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ChunkIndex
    {
        internal int Value;

        public static ChunkIndex Null => default;

        public int ListIndex => ToUnity().ListIndex;
        public byte* Buffer => ToUnity().Buffer;
        public Entity MetaChunkEntity => ToUnity().MetaChunkEntity;

        internal UnityEntities.ChunkIndex ToUnity() => UnsafeUtility.As<ChunkIndex, UnityEntities.ChunkIndex>(ref this);

        internal static ChunkIndex From(UnityEntities.ChunkIndex chunk) => UnsafeUtility.As<UnityEntities.ChunkIndex, ChunkIndex>(ref chunk);
    }
}