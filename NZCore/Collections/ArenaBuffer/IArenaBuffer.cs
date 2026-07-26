// <copyright project="NZCore" file="IArenaBuffer.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore
{
    /// <summary>
    /// Marker interface for arena buffer elements.
    /// Implement this on your element struct to get an <see cref="ArenaBuffer{T}"/> instead of a DynamicBuffer.
    ///
    /// Unlike a DynamicBuffer - which stores elements inline in chunk memory up to the internal capacity and
    /// heap allocates one block per entity beyond it - every buffer of a given element type lives in a single
    /// contiguous arena. The entity's chunk only holds a 12 byte <see cref="ArenaBufferRefData"/> record.
    ///
    /// <code><![CDATA[
    /// public struct MyArenaBuffer : IArenaBuffer
    /// {
    ///     public float Value1;
    ///     public int Value2;
    /// }
    ///
    /// // in a baker
    /// baker.AddArenaBuffer<MyArenaBuffer>(entity, 16);
    ///
    /// // in a system
    /// var buffer = _lookup[entity];
    /// buffer.Add(new MyArenaBuffer { Value1 = 1 });
    /// ]]></code>
    ///
    /// A source generator emits the companion <c>MyArenaBufferRef</c> component, the lookup and type handle
    /// accessors and the arena registration for every type implementing this interface.
    ///
    /// Freeing is not automatic: there is no cleanup component, so a destroy pipeline has to release the
    /// blocks with <see cref="ArenaBufferReleaseHandles"/> while the components are still on the entities.
    /// <c>ArenaBufferReserveSystem</c> frees whatever is left when the World is torn down, and reports blocks
    /// whose component was removed without being released.
    /// </summary>
    public interface IArenaBuffer
    {
    }

    /// <summary>
    /// Which allocator backs an element type's arena. Both carve blocks the same way - power of two size
    /// classes, free lists threaded through the free blocks themselves - and differ only in where the memory
    /// comes from and therefore how a handle resolves to an address.
    /// </summary>
    public enum ArenaAllocatorMode
    {
        /// <summary>
        /// Fixed size pages that are never moved. A handle resolves through the page table, which costs one
        /// dependent load. Growing one buffer can never invalidate another buffer's memory, so a pointer from
        /// <c>GetUnsafePtr</c> stays valid until <b>that</b> buffer grows.
        /// </summary>
        Paged = 0,

        /// <summary>
        /// One contiguous block that is reallocated when it fills up. A handle is a plain byte offset, so it
        /// resolves with an add instead of a table lookup.
        ///
        /// The trade is pointer lifetime: a reallocation moves the whole arena, so a pointer or NativeArray
        /// held across a growth of <b>any</b> buffer of this element type dangles. Handles and the free lists
        /// survive, because both are offsets rather than addresses.
        /// </summary>
        Contiguous = 1,

        /// <summary>
        /// Pages again, but one per reserved entity chunk instead of a fixed size. Every buffer in a chunk is
        /// on the chunk's page, so a chunk accessor resolves the page once and each buffer costs a compare and
        /// an add - the page table load happens once per chunk rather than once per entity.
        ///
        /// A chunk's page uses a single size class, taken from the largest capacity requested in that chunk,
        /// so buffers that asked for less are padded up to it. Reaching a buffer through a lookup instead of a
        /// chunk gets no benefit at all, since there is no chunk page to inherit.
        ///
        /// Pointer lifetime matches <see cref="Paged"/>: pages never move.
        /// </summary>
        ChunkPaged = 2,

        /// <summary>
        /// <see cref="ChunkPaged"/>, but the page is shared by every element type in this mode rather than
        /// being per type. A chunk gets one page holding all of its arena buffer types, one contiguous run
        /// each, so an entity carrying several arena buffers has them within a page of each other.
        ///
        /// All types in this mode share a single process wide arena, which is what makes the sharing possible
        /// without declaring groups. That arena is byte oriented, so a type's element size travels with its
        /// registration instead of living in the allocator.
        ///
        /// Same padding cost as <see cref="ChunkPaged"/>, applied per type: within a chunk, every buffer of a
        /// given type is padded to the largest capacity requested for that type in that chunk.
        /// </summary>
        SharedChunkPaged = 3
    }

    /// <summary>
    /// Optional. Sets the initial element capacity of the arena backing this element type, and which
    /// allocator backs it. The arena grows on demand either way, so the capacity only avoids early
    /// reallocations - though it matters more under <see cref="ArenaAllocatorMode.Contiguous"/>, where a
    /// growth moves memory.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public class ArenaBufferAttribute : Attribute
    {
        public int InitialCapacity = 1024;

        public ArenaAllocatorMode Mode = ArenaAllocatorMode.Paged;
    }
}
