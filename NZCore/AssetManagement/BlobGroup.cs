// <copyright project="NZCore" file="BlobGroup.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    public struct BlobGroup<T>
        where T : unmanaged
    {
        public BlobArray<T> Items; // indexed is AutoID; gaps are zeroed
    }
}
