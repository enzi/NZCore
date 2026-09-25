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
        public AnimatorOverrideFlags Flags;

        public WeakObjectReference<AnimationClip> AnimationClip => _animationClip;
        public float Speed => _speed;
        public bool IsOwned => (Flags & AnimatorOverrideFlags.Owned) != 0;

        public void SetClip(WeakObjectReference<AnimationClip> clip, float speed)
        {
            State = AnimatorOverrideEnum.Requested;

            _animationClip = clip;
            _speed = speed;
            Flags = AnimatorOverrideFlags.None;
        }

        public void Clear()
        {
            _animationClip = default;
            State = AnimatorOverrideEnum.Default;
            Flags = AnimatorOverrideFlags.None;
        }
    }

    [System.Flags]
    public enum AnimatorOverrideFlags : byte
    {
        None = 0,

        /// <summary> Owned by another system: clip requests are ignored, and the owner drives the clip's time and ends it. </summary>
        Owned = 1 << 0
    }

    public enum AnimatorOverrideEnum : byte
    {
        Default,
        Requested,
        Playing
    }
}
