// <copyright project="NZCore" file="NZDestroySystemGroup.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class NZDestroySystemGroup : ComponentSystemGroup
    {
    }
}