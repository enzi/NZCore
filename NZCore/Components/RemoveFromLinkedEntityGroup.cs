// <copyright project="NZCore" file="RemoveFromLinkedEntityGroup.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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