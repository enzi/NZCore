// <copyright project="NZCore" file="ArenaBufferStorage.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Burst;
using Unity.Entities;

namespace NZCore
{
    /// <summary>Context type for the per element type <see cref="Unity.Burst.SharedStatic{T}"/> keys.</summary>
    public struct ArenaBufferStorageKey
    {
    }

    /// <inheritdoc cref="ArenaBufferStorageKey"/>
    public struct ArenaBufferRefTypeIndexKey
    {
    }

    /// <inheritdoc cref="ArenaBufferStorageKey"/>
    public struct ArenaBufferRefArenaKey
    {
    }

    /// <inheritdoc cref="ArenaBufferStorageKey"/>
    public struct ArenaBufferRefElementSizeKey
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

        /// <summary>
        /// TypeIndex of the generated <c>*Ref</c> component for this element type. Lets the untyped
        /// add/set paths reach the concrete component without a managed lookup.
        /// </summary>
        public static readonly SharedStatic<TypeIndex> RefTypeIndex = SharedStatic<TypeIndex>.GetOrCreate<ArenaBufferRefTypeIndexKey, T>();

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
    }

    /// <summary>
    /// The same arena as <see cref="ArenaBufferStorage{T}"/>, keyed by the generated <c>*Ref</c> component
    /// instead of the element type, plus the element size that goes with it.
    ///
    /// Generic code parameterised on a ref component alone - a system shared by every stat set, say - can
    /// name <c>ComponentLookup&lt;TRef&gt;</c> but has no way to name the element type, so it cannot reach
    /// <see cref="ArenaBufferStorage{T}"/>. These two are what it needs to build an
    /// <see cref="ArenaByteBuffer"/> over a record. Both are published by
    /// <see cref="ArenaBufferRegistry.Register{TElement,TRef}"/>, which has both types in hand.
    /// </summary>
    public static class ArenaBufferRefStorage<TRef>
        where TRef : unmanaged, IComponentData
    {
        public static readonly SharedStatic<IntPtr> Arena = SharedStatic<IntPtr>.GetOrCreate<ArenaBufferRefArenaKey, TRef>();

        public static readonly SharedStatic<int> ElementSize = SharedStatic<int>.GetOrCreate<ArenaBufferRefElementSizeKey, TRef>();

        public static unsafe ArenaAllocator* GetArena()
        {
            return (ArenaAllocator*)Arena.Data;
        }
    }
}
