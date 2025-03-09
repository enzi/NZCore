// <copyright project="NZCore" file="WeakReferenceAssetBuffer.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
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