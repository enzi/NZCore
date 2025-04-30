// <copyright project="NZCore" file="BeginSimulationSystemGroup.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using Unity.NetCode;

namespace NZCore
{
    [UpdateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(VariableRateSimulationSystemGroup))]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
#if UNITY_NETCODE
    [UpdateAfter(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
#endif
    public partial class BeginSimulationSystemGroup : ComponentSystemGroup
    {
    }
}