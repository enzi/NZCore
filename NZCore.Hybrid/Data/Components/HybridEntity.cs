// <copyright project="NZCore.Hybrid.Data" file="HybridEntity.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using Unity.Entities.Content;
using UnityEngine;

namespace NZCore.Hybrid
{
    public struct EnableHybridPresentation : IComponentData, IEnableableComponent, ISavable { }
    public struct HybridPresentationEnabled : IComponentData, IEnableableComponent { }

    public struct HybridPresentation : IComponentData, ISavable
    {
        public WeakObjectReference<GameObject> Prefab;
    }
}