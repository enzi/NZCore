// <copyright project="NZCore" file="EndGhostSimulationBarrier.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class EndGhostSimulationBarrier : EntityCommandBufferSystem
    {
    }    
}
