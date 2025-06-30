// <copyright project="NZCore.Hybrid" file="HybridAudioSingleton.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.Interfaces;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Mathematics;
using UnityEngine;

namespace NZCore.Hybrid
{
    public struct HybridAudioSingleton : IInitSingleton, IDisposable
    {
        public ParallelList<HybridAudioRequest> Requests;

        public void Init()
        {
            Requests = new ParallelList<HybridAudioRequest>(0, Allocator.Persistent);
        }
        
        public void Dispose()
        {
            Requests.Dispose();
        }
    }

    public struct HybridAudioRequest
    {
        public WeakObjectReference<AudioClip> Clip;
        public float3 Position;
        public Entity FollowEntity;
        public float Volume;
        public bool is3d;
    }
}