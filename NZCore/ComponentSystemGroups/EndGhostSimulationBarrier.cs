using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class EndGhostSimulationBarrier : EntityCommandBufferSystem
{
}

