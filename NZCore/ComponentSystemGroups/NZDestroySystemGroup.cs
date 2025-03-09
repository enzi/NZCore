// <copyright project="NZCore" file="NZDestroySystemGroup.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class NZDestroySystemGroup : ComponentSystemGroup
    {
    }
}