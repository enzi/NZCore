// <copyright project="NZCore.Hybrid" file="HybridObjectBuffer.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using UnityEngine;

namespace NZCore.Hybrid
{
    [InternalBufferCapacity(0)]
    public struct HybridObjectBuffer : IBufferElementData
    {
        public UnityObjectRef<GameObject> GameObject;
    }
}