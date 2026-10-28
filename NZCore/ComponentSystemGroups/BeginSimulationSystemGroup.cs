// <copyright project="NZCore" file="BeginSimulationSystemGroup.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    [UpdateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(VariableRateSimulationSystemGroup))]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
#if UNITY_NETCODE
    [UpdateAfter(typeof(Unity.Netcode.GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(Unity.Netcode.PredictedSimulationSystemGroup))]
#endif
    public partial class BeginSimulationSystemGroup : ComponentSystemGroup { }
}