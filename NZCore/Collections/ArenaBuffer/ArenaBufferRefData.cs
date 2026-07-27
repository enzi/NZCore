// <copyright project="NZCore" file="ArenaBufferRefData.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;

namespace NZCore
{
    /// <summary>
    /// The canonical layout every generated <c>*Ref</c> component mirrors. Because all of them share this
    /// layout, untyped code can reinterpret a chunk's component array to this struct without knowing the
    /// concrete type.
    ///
    /// The generated <c>*Ref</c> is the single source of truth for a buffer's block - there is no second copy
    /// to keep in sync when a buffer grows. There is deliberately no cleanup component either: freeing is the
    /// destroy pipeline's job, which releases blocks through <see cref="ArenaBufferReleaseHandles"/> while the
    /// components are still on the entities.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct ArenaBufferRefData
    {
        /// <summary>
        /// Where the block is. Which of the two readings below applies is decided by the allocator mode, and
        /// a record is only ever touched by code that already knows its mode.
        ///
        /// <see cref="ArenaAllocatorMode.Paged"/> stores the block <b>address</b> here. Its pages never move,
        /// so the address stays valid for the block's whole lifetime, and reaching the elements costs nothing
        /// beyond the load of this field - which is already in the cache line the caller is reading. That is
        /// the entire reason this field is eight bytes wide.
        ///
        /// Every other mode stores a packed <see cref="Handle"/> instead, because their blocks can move:
        /// Contiguous reallocates its single backing block, and the chunk paged modes evict a buffer from its
        /// chunk page when it outgrows it.
        /// </summary>
        [FieldOffset(0)]
        public IntPtr Block;

        /// <summary>
        /// The handle reading of <see cref="Block"/>, for the modes whose blocks move. Overlaid on the low
        /// half rather than converted from it: this is read on the hot path of every handle mode, and a
        /// property doing (int)Block cost those modes over 2x on the append benchmark.
        ///
        /// Assigning this leaves the high half holding whatever was there before, which is harmless because
        /// no handle mode ever reads <see cref="Block"/> as an address.
        /// </summary>
        [FieldOffset(0)]
        public int Handle;

        /// <summary>Number of elements currently in the buffer.</summary>
        [FieldOffset(8)]
        public int Length;

        /// <summary>
        /// Number of elements the block can hold, always a power of two. While unreserved this carries the
        /// capacity the record is asking for.
        /// </summary>
        [FieldOffset(12)]
        public int Capacity;

        /// <summary>
        /// Value meaning "no arena block reserved yet". Deliberately -1 rather than 0 so that it reads the
        /// same whether <see cref="Block"/> is holding a handle or an address: no allocation ever lands on
        /// the all-ones address, so one comparison covers both modes and <see cref="IsReserved"/> does not
        /// need to know which one it is looking at.
        /// </summary>
        public const int Unreserved = -1;

        /// <summary>
        /// Testing the low half covers both readings, and is provably safe for the address reading: every
        /// block is at least eight byte aligned, so its low three bits are zero, while the sentinel's are
        /// all ones. No real block address can collide with it.
        /// </summary>
        public readonly bool IsReserved => Handle != Unreserved;
    }
}
