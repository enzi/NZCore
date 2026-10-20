// <copyright project="Unity.Entities" file="UnsafeMatchingArchetypePtrList.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Keep the field order identical to Unity's list; it is copied by value into job structs.
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct UnsafeMatchingArchetypePtrList
    {
        [NativeDisableUnsafePtrRestriction] internal UnsafeList<IntPtr>* ListData;
        [NativeDisableUnsafePtrRestriction] public EntityComponentStore* entityComponentStore;

        private ref UnityEntities.UnsafeMatchingArchetypePtrList Self =>
            ref UnsafeUtility.AsRef<UnityEntities.UnsafeMatchingArchetypePtrList>(UnsafeUtility.AddressOf(ref this));

        public MatchingArchetype** Ptr => (MatchingArchetype**)Self.Ptr;
    }
}