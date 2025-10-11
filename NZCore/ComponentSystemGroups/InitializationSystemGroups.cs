// <copyright project="NZCore" file="HybridInitializationSystemGroup.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class NZCoreInitializationSystemGroup : ComponentSystemGroup
    {
    }
    
    // [UpdateInGroup(typeof(InitializationSystemGroup))]
    // public partial class HybridInitializationSystemGroup : ComponentSystemGroup
    // {
    // }
}