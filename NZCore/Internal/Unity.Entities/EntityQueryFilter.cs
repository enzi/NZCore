// <copyright project="Unity.Entities" file="EntityQueryFilter.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Keep the field order identical to Unity's filter; it is copied by value into job structs.
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityQueryFilter
    {
        public struct SharedComponentData
        {
            public const int Capacity = UnityEntities.EntityQueryFilter.SharedComponentData.Capacity;

            public int Count;
            public fixed int IndexInEntityQuery[Capacity];
            public fixed int SharedComponentIndex[Capacity];
        }

        public struct ChangedFilter
        {
            public const int Capacity = UnityEntities.EntityQueryFilter.ChangedFilter.Capacity;

            public int Count;
            public fixed int IndexInEntityQuery[Capacity];
        }

        public uint RequiredChangeVersion;
        public SharedComponentData Shared;
        public ChangedFilter Changed;
        internal uint _UseOrderFiltering;

        private ref UnityEntities.EntityQueryFilter Self => ref UnsafeUtility.AsRef<UnityEntities.EntityQueryFilter>(UnsafeUtility.AddressOf(ref this));

        public bool RequiresMatchesFilter => Self.RequiresMatchesFilter;

        internal static ref UnityEntities.EntityQueryFilter AsUnity(ref EntityQueryFilter filter) =>
            ref UnsafeUtility.As<EntityQueryFilter, UnityEntities.EntityQueryFilter>(ref filter);
    }
}