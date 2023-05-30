using Unity.Entities;

//[UpdateAfter(typeof(GhostSimulationSystemGroup))]
//[UpdateInWorld(UpdateInWorld.TargetWorld.ClientAndServer)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class EndGhostSimulationBarrier : EntityCommandBufferSystem
{
}

