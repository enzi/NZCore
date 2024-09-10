// <copyright project="NZCore" file="IBlobAssetReference.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Entities;

namespace NZCore
{
    public interface IBlobAssetReference<T> : IComponentData
        where T : unmanaged
    {
        public BlobAssetReference<T> blob { get; set; }
        public Guid guid { get; set; }
    }

    public interface IBlobAssetReference<T1, T2> : IComponentData
        where T1 : unmanaged
        where T2 : unmanaged
    {
        public BlobAssetReference<T1> blob1 { get; set; }
        public BlobAssetReference<T2> blob2 { get; set; }
        public Guid guid { get; set; }
    }
}