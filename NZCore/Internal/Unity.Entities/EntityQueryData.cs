// <copyright project="Unity.Entities" file="EntityQueryData.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Engine-owned and only reached through pointers. Wraps Unity's query data so the layout stays identical.
    public unsafe struct EntityQueryData
    {
        internal UnityEntities.EntityQueryData Value;

        private ref UnityEntities.EntityQueryData Self => ref UnsafeUtility.AsRef<UnityEntities.EntityQueryData>(UnsafeUtility.AddressOf(ref Value));

        public ComponentType* RequiredComponents => Value.RequiredComponents;
        public byte HasEnableableComponents => Value.HasEnableableComponents;

        public ref UnsafeMatchingArchetypePtrList MatchingArchetypes =>
            ref UnsafeUtility.As<UnityEntities.UnsafeMatchingArchetypePtrList, UnsafeMatchingArchetypePtrList>(ref Self.MatchingArchetypes);
    }
}