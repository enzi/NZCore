// <copyright project="NZCore.UI.Editor" file="DeferredGizmo.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Mathematics;
using UnityEngine;

namespace NZCore.UI.Editor
{
    public enum GizmoType
    {
        Sphere,
        Capsule,
        Box,
        Circle,
        Cylinder
    }

    public class DeferredGizmo
    {
        public Color Color;
        public GizmoType Type;
        public float3 Position;
        public Quaternion Rotation;
        public float3 Scale;
        public float3 Size;

        // public float Radius;
        // public float Height;
        // public float Length;
    }
}