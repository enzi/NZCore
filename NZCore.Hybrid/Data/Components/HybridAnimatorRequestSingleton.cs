// <copyright project="NZCore.Hybrid.Data" file="HybridAnimatorRequestSingleton.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.Interfaces;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Content;
using UnityEngine;

namespace NZCore.Hybrid
{
    public struct HybridAnimatorRequestSingleton : IInitSingleton, IDisposable
    {
        public ParallelList<HybridAnimatorClipRequest> ClipRequests;

        public void Init()
        {
            ClipRequests = new ParallelList<HybridAnimatorClipRequest>(0, Allocator.Persistent);
        }

        public void Dispose()
        {
            ClipRequests.Dispose();
        }
    }
    
    public struct HybridAnimatorClipRequest
    {
        public Entity Entity;
        public WeakObjectReference<AnimationClip> Clip;
        public float Speed;
    }
}