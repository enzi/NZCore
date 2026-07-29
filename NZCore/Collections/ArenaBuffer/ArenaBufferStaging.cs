// <copyright project="NZCore" file="ArenaBufferStaging.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    /// <summary>
    /// Temporary DynamicBuffer storage used by bakers and ECBs until ArenaBufferReserveSystem copies the
    /// elements into their arena block. The source generator registers each closed generic type.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ArenaBufferStaging<T> : IBufferElementData where T : unmanaged, IArenaBuffer
    {
        public T Value;
    }
}
