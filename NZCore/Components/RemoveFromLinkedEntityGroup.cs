// <copyright project="NZCore" file="RemoveFromLinkedEntityGroup.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore.Components
{
    public struct RemoveFromLinkedEntityGroupCleanupSetup : IComponentData
    {
        public Entity Parent;
    }

    public struct RemoveFromLinkedEntityGroupCleanup : ICleanupComponentData
    {
        public Entity Parent;
    }
}