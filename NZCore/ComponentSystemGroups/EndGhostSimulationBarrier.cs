// <copyright project="NZCore" file="EndGhostSimulationBarrier.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class EndGhostSimulationBarrier : EntityCommandBufferSystem
    {
    }    
}
