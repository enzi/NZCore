// <copyright project="NZCore" file="EndGhostSimulationBarrier.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class EndGhostSimulationBarrier : EntityCommandBufferSystem
{
}