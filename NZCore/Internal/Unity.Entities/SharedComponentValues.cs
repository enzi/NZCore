// <copyright project="Unity.Entities" file="SharedComponentValues.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Keep the field order identical to Unity's shared component value view.
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SharedComponentValues
    {
        public int* firstIndex;
        public int stride;

        private ref UnityEntities.SharedComponentValues Self =>
            ref UnsafeUtility.AsRef<UnityEntities.SharedComponentValues>(UnsafeUtility.AddressOf(ref this));

        public ref int this[int i] => ref Self[i];

        internal static SharedComponentValues From(UnityEntities.SharedComponentValues values) =>
            UnsafeUtility.As<UnityEntities.SharedComponentValues, SharedComponentValues>(ref values);
    }
}