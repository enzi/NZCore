// <copyright project="NZCore" file="RemoveFromLinkedEntityGroup.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
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