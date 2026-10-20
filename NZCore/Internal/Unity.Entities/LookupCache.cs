// <copyright project="Unity.Entities" file="LookupCache.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Keep the field order identical to Unity's cache; it is embedded in layout-cast lookups and type handles.
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct LookupCache
    {
        [NativeDisableUnsafePtrRestriction] public Archetype* Archetype;
        public int ComponentOffset;
        public ushort ComponentSizeOf;
        public short IndexInArchetype;

        private ref UnityEntities.LookupCache Self => ref UnsafeUtility.AsRef<UnityEntities.LookupCache>(UnsafeUtility.AddressOf(ref this));

        public void Update(Archetype* archetype, TypeIndex typeIndex) => Self.Update((UnityEntities.Archetype*)archetype, typeIndex);

        internal static ref UnityEntities.LookupCache AsUnity(ref LookupCache cache) => ref UnsafeUtility.As<LookupCache, UnityEntities.LookupCache>(ref cache);
    }
}