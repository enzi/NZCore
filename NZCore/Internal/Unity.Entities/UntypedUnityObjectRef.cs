// <copyright project="Unity.Entities" file="UntypedUnityObjectRef.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Keep the serialized field name and layout identical to Unity's untyped object ref.
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct UntypedUnityObjectRef
    {
#if UNITY_6000_4_OR_NEWER
        [SerializeField] public EntityId entityId;
#else
        [SerializeField] public int instanceId;
#endif

        internal static UntypedUnityObjectRef From(UnityEntities.UntypedUnityObjectRef id) =>
            UnsafeUtility.As<UnityEntities.UntypedUnityObjectRef, UntypedUnityObjectRef>(ref id);

        internal UnityEntities.UntypedUnityObjectRef ToUnity() => UnsafeUtility.As<UntypedUnityObjectRef, UnityEntities.UntypedUnityObjectRef>(ref this);
    }
}