// <copyright project="NZCore.Hybrid.Data" file="CreateHybridObjectRequest.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;
using NZCore.Interfaces;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Mathematics;
using UnityEngine;

namespace NZCore.Hybrid
{
    public struct CreateHybridObjectRequestSingleton : IInitSingleton, IDisposable
    {
        //public NativeList<SpawnParticleData> SpawnParticlesList;
        public ParallelList<CreateHybridObjectRequest> Requests;
        
        public void Init()
        {
            Requests = new ParallelList<CreateHybridObjectRequest>(0, Allocator.Persistent);
        }

        public void Dispose()
        {
            //SpawnParticlesList.Dispose();
            Requests.Dispose();
        }
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct CreateHybridObjectRequest
    {
        [FieldOffset(0)] public WeakObjectReference<GameObject> PrefabToLoad; // 48 bytes
        [FieldOffset(48)] public half DestroyTime;
        [FieldOffset(50)] public Entity BindToEntity;

        [FieldOffset(58)] public CreateHybridSpawnMode SpawnMode;

        // VFXSpawnMode.Position
        [FieldOffset(59)] public HybridPositionData PositionData;

        // VFXSpawnMode.Locator
        [FieldOffset(59)] public HybridLocatorData LocatorData;
    }

    public struct HybridPositionData
    {
        public float3 Position;
        public float3 Offset;
    }
    
    public struct HybridLocatorData
    {
        public Entity Entity;
        public LocatorPosition Locator;
        public bool AttachToParent;
    }
    
    public enum LocatorPosition : byte
    {
        None,
        Head,
        HandLeft,
        HandRight,
        Spine,
        FeetLeft,
        FeetRight,
        FeetBetween,
        WeaponLeft,
        WeaponRight
    }
    
    public enum CreateHybridSpawnMode : byte
    {
        Position,
        Locator
    }
}