// <copyright project="Unity.Entities" file="ComponentDependencyManager.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections.LowLevel.Unsafe;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Engine-owned and only reached through pointers. Wraps Unity's manager so the layout stays identical.
    public unsafe struct ComponentDependencyManager
    {
        internal UnityEntities.ComponentDependencyManager Value;

        private ref UnityEntities.ComponentDependencyManager Self =>
            ref UnsafeUtility.AsRef<UnityEntities.ComponentDependencyManager>(UnsafeUtility.AddressOf(ref Value));

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public ref ComponentSafetyHandles Safety => ref UnsafeUtility.As<UnityEntities.ComponentSafetyHandles, ComponentSafetyHandles>(ref Self.Safety);
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        public ref ForEachDisallowStructuralChangeSupport ForEachStructuralChange =>
            ref UnsafeUtility
                .As<UnityEntities.ForEachDisallowStructuralChangeSupport, ForEachDisallowStructuralChangeSupport>(ref Self.ForEachStructuralChange);
#endif
    }
}