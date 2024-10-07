// <copyright project="NZCore" file="StructuralCommandBufferSystem.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateBefore(typeof(BeginSimulationEntityCommandBufferSystem))]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial class StructuralCommandBufferSystem : EntityCommandBufferSystem
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            this.RegisterSingleton<Singleton>(ref PendingBuffers, World.Unmanaged);
        }

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
    }
}