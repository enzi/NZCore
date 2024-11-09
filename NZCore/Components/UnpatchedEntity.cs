// <copyright project="NZCore" file="UnpatchedEntity.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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