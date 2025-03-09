// <copyright project="NZCore" file="IBlobIndex.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections;

namespace NZCore.Interfaces
{
    public interface IBlobIndex
    {
        public int BlobIndex { get; }
    }

    public interface IBlobIndexList<T>
        where T : unmanaged
    {
        public NativeList<T> GetIndexList();
    }
}