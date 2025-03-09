// <copyright project="NZCore" file="UnpatchedEntity.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore.Components
{
    public struct UnpatchedEntity : IComponentData
    {
        public int Index;
        public int Version;
    }
}