// <copyright project="Unity.Entities" file="ForEachDisallowStructuralChangeSupport.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Lives inside ComponentDependencyManager. Wraps Unity's tracking so the layout stays identical; all calls delegate to Unity.
    public unsafe struct ForEachDisallowStructuralChangeSupport
    {
        internal UnityEntities.ForEachDisallowStructuralChangeSupport Value;

        public void BeginIsInForEach(EntityQueryImpl* query) => Value.BeginIsInForEach((UnityEntities.EntityQueryImpl*)query);

        public void EndIsInForEach() => Value.EndIsInForEach();
    }
}
#endif