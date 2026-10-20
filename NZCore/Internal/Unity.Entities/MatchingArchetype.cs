// <copyright project="Unity.Entities" file="MatchingArchetype.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Engine-owned and only passed through pointers to Unity's chunk iteration.
    public struct MatchingArchetype
    {
        internal UnityEntities.MatchingArchetype Value;
    }
}