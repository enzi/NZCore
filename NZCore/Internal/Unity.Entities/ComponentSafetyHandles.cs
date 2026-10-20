// <copyright project="Unity.Entities" file="ComponentSafetyHandles.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

#if ENABLE_UNITY_COLLECTIONS_CHECKS
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Lives inside ComponentDependencyManager. Wraps Unity's safety handles so the layout stays identical; all calls delegate to Unity.
    public struct ComponentSafetyHandles
    {
        internal UnityEntities.ComponentSafetyHandles Value;

        public AtomicSafetyHandle GetSafetyHandleForComponentLookup(TypeIndex type, bool isReadOnly) =>
            Value.GetSafetyHandleForComponentLookup(type, isReadOnly);

        public AtomicSafetyHandle GetBufferHandleForBufferLookup(TypeIndex type) => Value.GetBufferHandleForBufferLookup(type);

        public AtomicSafetyHandle GetSafetyHandleForDynamicComponentTypeHandle(TypeIndex type, bool isReadOnly) =>
            Value.GetSafetyHandleForDynamicComponentTypeHandle(type, isReadOnly);

        public AtomicSafetyHandle GetBufferHandleForBufferTypeHandle(TypeIndex type) => Value.GetBufferHandleForBufferTypeHandle(type);

        public AtomicSafetyHandle GetSafetyHandle(TypeIndex type, bool isReadOnly) => Value.GetSafetyHandle(type, isReadOnly);

        public AtomicSafetyHandle GetBufferSafetyHandle(TypeIndex type) => Value.GetBufferSafetyHandle(type);
    }
}
#endif