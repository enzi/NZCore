// <copyright project="NZCore" file="ArenaBufferRefData.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

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
    [StructLayout(LayoutKind.Sequential)]
    public struct ArenaBufferRefData
    {
        /// <summary>
        /// Packed handle of the block: high bits index the arena's page table, low bits are the byte offset
        /// into that page. <see cref="Unreserved"/> while no block has been handed out yet.
        /// </summary>
        public int Handle;

        /// <summary>Number of elements currently in the buffer.</summary>
        public int Length;

        /// <summary>
        /// Number of elements the block can hold, always a power of two. While unreserved this carries the
        /// capacity the record is asking for.
        /// </summary>
        public int Capacity;

        /// <summary><see cref="Handle"/> value meaning "no arena block reserved yet".</summary>
        public const int Unreserved = -1;

        public readonly bool IsReserved => Handle != Unreserved;
    }
}
