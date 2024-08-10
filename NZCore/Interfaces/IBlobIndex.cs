// <copyright project="NZCore" file="IBlobIndex.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
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