// <copyright project="NZCore" file="WeakReferenceAssetBuffer.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

namespace NZCore
{
    public struct WeakReferenceAssetBuffer : IBufferElementData
    {
        public UntypedWeakReferenceId Id;
    }
    
    public struct UnityObjectReferenceBuffer : IBufferElementData
    {
        public UnityObjectRef<Object> Asset;
    }
}