// <copyright project="NZCore" file="AnimatorOverride.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using Unity.Entities.Content;
using UnityEngine;

namespace NZCore.Hybrid
{
    public struct AnimatorOverride : IComponentData
    {
        private WeakObjectReference<AnimationClip> _animationClip;
        private float _speed;
        public AnimatorOverrideEnum State;

        public WeakObjectReference<AnimationClip> AnimationClip => _animationClip;
        public float Speed => _speed;

        public void SetClip(WeakObjectReference<AnimationClip> clip, float speed)
        {
            State = AnimatorOverrideEnum.Requested;
            
            _animationClip = clip;
            _speed = speed;
        }

        public void Clear()
        {
            _animationClip = default;
            State = AnimatorOverrideEnum.Default;
        }
    }

    public enum AnimatorOverrideEnum : byte
    {
        Default,
        Requested,
        Playing
    }
}