// <copyright project="NZSpellCasting.Hybrid.Data" file="AnimatorOverride.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using Unity.Entities.Content;
using UnityEngine;

namespace NZCore.Hybrid
{
    public struct AnimatorOverride : IComponentData
    {
        public WeakObjectReference<AnimationClip> AnimationClip;
        public float Scale;
    }

    public struct AnimatorOverrideState : IComponentData
    {
        public byte Playing;
    }
}