// <copyright project="Unity.Entities" file="UnsafeChunkCacheIterator.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Burst.Intrinsics;
using Unity.Entities;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Wraps Unity's iterator so the iteration and enabled-bit handling stay Unity's.
    public unsafe struct UnsafeChunkCacheIterator
    {
        internal UnityEntities.UnsafeChunkCacheIterator Value;

        public UnsafeChunkCacheIterator(in EntityQueryFilter filter, bool hasEnableableComponents, UnsafeCachedChunkList list,
            MatchingArchetype** matchingArchetypes)
        {
            var unityFilter = filter;
            Value = new UnityEntities.UnsafeChunkCacheIterator(in EntityQueryFilter.AsUnity(ref unityFilter), hasEnableableComponents,
                UnsafeCachedChunkList.AsUnity(ref list), (UnityEntities.MatchingArchetype**)matchingArchetypes);
        }

        public int Length
        {
            get => Value.Length;
            set => Value.Length = value;
        }

        public bool MoveNextChunk(ref int chunkIndexInCache, out ArchetypeChunk outputChunk, out int outputChunkEntityCount, out byte outputUseEnableBits,
            ref v128 enableBits) =>
            Value.MoveNextChunk(ref chunkIndexInCache, out outputChunk, out outputChunkEntityCount, out outputUseEnableBits, ref enableBits);
    }
}