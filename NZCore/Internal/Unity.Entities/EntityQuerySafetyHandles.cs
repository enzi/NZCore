// <copyright project="Unity.Entities" file="EntityQuerySafetyHandles.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

#if ENABLE_UNITY_COLLECTIONS_CHECKS
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Keep the fields identical to Unity's handles; the job system reads them by layout and by the read/write count names.
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityQuerySafetyHandles
    {
        internal AtomicSafetyHandle m_Safety0;
        internal AtomicSafetyHandle m_SafetyEnableable1;
        internal AtomicSafetyHandle m_SafetyEnableable2;
        internal AtomicSafetyHandle m_SafetyEnableable3;
        internal AtomicSafetyHandle m_SafetyEnableable4;
        internal AtomicSafetyHandle m_SafetyEnableable5;
        internal AtomicSafetyHandle m_SafetyEnableable6;
        internal AtomicSafetyHandle m_SafetyEnableable7;
        internal AtomicSafetyHandle m_SafetyEnableable8;
        internal int m_SafetyReadOnlyCount;
        internal int m_SafetyReadWriteCount;

        public EntityQuerySafetyHandles(EntityQueryImpl* queryImpl)
        {
            var handles = new UnityEntities.EntityQuerySafetyHandles((UnityEntities.EntityQueryImpl*)queryImpl);
            this = UnsafeUtility.As<UnityEntities.EntityQuerySafetyHandles, EntityQuerySafetyHandles>(ref handles);
        }
    }
}
#endif