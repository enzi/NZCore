using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    public partial class NZSpellCastingSimulationSystemGroup : ComponentSystemGroup
    {
    }


    [UpdateInGroup(typeof(NZSpellCastingSimulationSystemGroup))]
    public partial class Stage1SystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(NZSpellCastingSimulationSystemGroup))]
    [UpdateAfter(typeof(Stage1SystemGroup))]
    public partial class Stage2SystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(NZSpellCastingSimulationSystemGroup))]
    [UpdateAfter(typeof(Stage1SystemGroup))]
    public partial class NZStatsSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(Stage2SystemGroup))]
    public partial class EndStage2CommandBufferSystem : EntityCommandBufferSystem
    {
    }

    [UpdateInGroup(typeof(NZSpellCastingSimulationSystemGroup))]
    [UpdateAfter(typeof(Stage2SystemGroup))]
    public partial class Stage5SystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(NZSpellCastingSimulationSystemGroup))]
    [UpdateAfter(typeof(Stage5SystemGroup))]
    public partial class Stage6SystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(NZSpellCastingSimulationSystemGroup))]
    [UpdateAfter(typeof(Stage6SystemGroup))]
    public partial class Stage7SystemGroup : ComponentSystemGroup
    {
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    //[UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial class DestroyEntityCommandBufferSystem : EntityCommandBufferSystem
    {
        public unsafe struct Singleton : IComponentData, IECBSingleton
        {
            private UnsafeList<EntityCommandBuffer>* pendingBuffers;
            private AllocatorManager.AllocatorHandle allocator;

            public EntityCommandBuffer CreateCommandBuffer(WorldUnmanaged world)
            {
                return EntityCommandBufferSystem.CreateCommandBuffer(ref *pendingBuffers, allocator, world);
            }

            public void SetPendingBufferList(ref UnsafeList<EntityCommandBuffer> buffers)
            {
                pendingBuffers = (UnsafeList<EntityCommandBuffer>*)UnsafeUtility.AddressOf(ref buffers);
            }

            public void SetAllocator(Allocator allocatorIn)
            {
                allocator = allocatorIn;
            }

            public void SetAllocator(AllocatorManager.AllocatorHandle allocatorIn)
            {
                allocator = allocatorIn;
            }
        }
        
        protected override void OnCreate()
        {
            base.OnCreate();

            this.RegisterSingleton<Singleton>(ref PendingBuffers, World.Unmanaged);
        }
    }
}