// <copyright project="NZCore" file="InputSystemGroup.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    [WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
#if UNITY_NETCODE
    [UpdateInGroup(typeof(Unity.Netcode.GhostInputSystemGroup))]
#else
    [UpdateInGroup(typeof(BeginSimulationSystemGroup))]
#endif
    public partial class InputSystemGroup : ComponentSystemGroup { }

    [WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
    [UpdateAfter(typeof(InputSystemGroup))]
#if UNITY_NETCODE
    [UpdateInGroup(typeof(Unity.Netcode.GhostInputSystemGroup))]
#else
    [UpdateInGroup(typeof(BeginSimulationSystemGroup))]
#endif
    public partial class InputProcessSystemGroup : ComponentSystemGroup { }
}