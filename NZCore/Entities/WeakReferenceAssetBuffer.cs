// <copyright project="NZCore" file="WeakReferenceAssetBuffer.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using Unity.Entities.Serialization;

namespace NZCore
{
    public struct WeakReferenceAssetBuffer : IBufferElementData
    {
        public UntypedWeakReferenceId Id;
    }
}