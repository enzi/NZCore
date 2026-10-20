// <copyright project="Unity.Entities" file="UnsafeCachedChunkList.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Keep the field order identical to Unity's chunk cache; it is copied by value into job structs.
    [NoAlias]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct UnsafeCachedChunkList
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction]
        internal UnsafeList<ChunkIndex>* MatchingChunks;

        [NativeDisableUnsafePtrRestriction] [NoAlias]
        internal UnsafeList<int>* PerChunkMatchingArchetypeIndex;

        [NativeDisableUnsafePtrRestriction] [NoAlias]
        internal UnsafeList<int>* ChunkIndexInArchetype;

        [NativeDisableUnsafePtrRestriction] [NoAlias]
        public EntityComponentStore* EntityComponentStore;

        internal int CacheValid;

        private ref UnityEntities.UnsafeCachedChunkList Self =>
            ref UnsafeUtility.AsRef<UnityEntities.UnsafeCachedChunkList>(UnsafeUtility.AddressOf(ref this));

        public ChunkIndex* ChunkIndices => (ChunkIndex*)Self.ChunkIndices;
        public int Length => Self.Length;

        internal static UnsafeCachedChunkList From(UnityEntities.UnsafeCachedChunkList list) =>
            UnsafeUtility.As<UnityEntities.UnsafeCachedChunkList, UnsafeCachedChunkList>(ref list);

        internal static ref UnityEntities.UnsafeCachedChunkList AsUnity(ref UnsafeCachedChunkList list) =>
            ref UnsafeUtility.As<UnsafeCachedChunkList, UnityEntities.UnsafeCachedChunkList>(ref list);
    }
}