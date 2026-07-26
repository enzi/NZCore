// <copyright project="NZCore" file="ArenaBufferStorage.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Burst;

namespace NZCore
{
    /// <summary>Context type for the per element type <see cref="Unity.Burst.SharedStatic{T}"/> keys.</summary>
    public struct ArenaBufferStorageKey
    {
    }

    /// <summary>
    /// Holds the <see cref="ArenaAllocator"/> for one element type, reachable from Burst without a lookup.
    /// Populated by the generated registration hook.
    ///
    /// Note that this is process global rather than per World: two Worlds sharing the process share the
    /// arena of a given element type. Tests must call <see cref="ArenaBufferRegistry.ResetAll"/> between
    /// runs for that reason.
    /// </summary>
    public static class ArenaBufferStorage<T>
        where T : unmanaged, IArenaBuffer
    {
        public static readonly SharedStatic<IntPtr> Arena = SharedStatic<IntPtr>.GetOrCreate<ArenaBufferStorageKey, T>();

        public static unsafe ArenaAllocator* GetArena()
        {
            return (ArenaAllocator*)Arena.Data;
        }

        /// <summary>
        /// The same pointer as <see cref="GetArena"/>, typed for an element type registered with
        /// <see cref="ArenaAllocatorMode.Contiguous"/>. Which one is correct is fixed per element type by
        /// <see cref="ArenaBufferAttribute.Mode"/>, and the generated accessors call the matching one.
        /// </summary>
        public static unsafe ContiguousArenaAllocator* GetContiguousArena()
        {
            return (ContiguousArenaAllocator*)Arena.Data;
        }

        /// <inheritdoc cref="GetContiguousArena"/>
        public static unsafe ChunkPagedArenaAllocator* GetChunkPagedArena()
        {
            return (ChunkPagedArenaAllocator*)Arena.Data;
        }

        /// <summary>
        /// The process wide arena every <see cref="ArenaAllocatorMode.SharedChunkPaged"/> type points at.
        /// Unlike the others this is the same object for every such element type, which is what lets a chunk
        /// hold buffers of different types on one page.
        /// </summary>
        public static unsafe SharedArenaAllocator* GetSharedArena()
        {
            return (SharedArenaAllocator*)Arena.Data;
        }
    }
}
