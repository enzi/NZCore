// <copyright project="NZCore" file="ArenaBufferSafety.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    /// <summary>
    /// The bridge the generated arena buffer accessors use to reach Unity.Entities internals.
    ///
    /// Generated code lives in the user's assembly and cannot touch those internals, while NZCore can.
    /// Everything here is a one line forward - keep it that way.
    /// </summary>
    public static class ArenaBufferSafety
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        /// <summary>The handle guarding the component a lookup reads, so the buffer checks against the same one.</summary>
        public static AtomicSafetyHandle Get<T>(ref ComponentLookup<T> lookup)
            where T : unmanaged, IComponentData
        {
            return lookup.m_Safety;
        }

        /// <inheritdoc cref="Get{T}(ref ComponentLookup{T})"/>
        public static AtomicSafetyHandle Get<T>(ref ComponentTypeHandle<T> typeHandle)
            where T : unmanaged, IComponentData
        {
            return typeHandle.m_Safety;
        }
#endif

        /// <summary>
        /// Builds a lookup straight off the EntityManager, for managed code and tests that have no
        /// SystemState at hand. Unlike the SystemState overload this registers no reader or writer, so the
        /// caller is responsible for job dependencies.
        /// </summary>
        public static ComponentLookup<T> GetComponentLookup<T>(EntityManager entityManager, bool isReadOnly)
            where T : unmanaged, IComponentData
        {
            return entityManager.GetComponentLookup<T>(isReadOnly);
        }
    }
}
