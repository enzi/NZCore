// <copyright project="NZCore" file="FixedStepUpdatedSystem.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

#if DOTS_PHYSICS
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;

namespace NZCore.Physics
{
    public struct AlwaysUpdatePhysicsWorld : IComponentData
    {
        internal bool FixedStepUpdatedThisFrame;
    }


    [UpdateInGroup(typeof(PhysicsInitializeGroup))]
    public partial struct FixedStepUpdatedSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AlwaysUpdatePhysicsWorld>();

            state.EntityManager.AddComponent<AlwaysUpdatePhysicsWorld>(state.SystemHandle);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.SetSingleton(new AlwaysUpdatePhysicsWorld
            {
                FixedStepUpdatedThisFrame = true
            });
        }
    }

    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [CreateAfter(typeof(BuildPhysicsWorld))]
    public unsafe partial class AlwaysUpdatePhysicsWorldSystem : SystemBase
    {
        private SystemHandle _buildPhysicsWorld;
        private SystemState* _buildPhysicsWorldSystemState;

        protected override void OnCreate()
        {
            _buildPhysicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();
            _buildPhysicsWorldSystemState = World.Unmanaged.ResolveSystemStateChecked(_buildPhysicsWorld);

            CheckedStateRef.AddSystemWriteDependency(TypeManager.GetTypeIndex<PhysicsWorldSingleton>());
        }

        protected override void OnUpdate()
        {
            var physicsUpdated = SystemAPI.GetSingletonRW<AlwaysUpdatePhysicsWorld>();

            if (physicsUpdated.ValueRO.FixedStepUpdatedThisFrame)
            {
                physicsUpdated.ValueRW.FixedStepUpdatedThisFrame = false;
                return;
            }

            _buildPhysicsWorld.Update(World.Unmanaged);

            var physicsDependency = _buildPhysicsWorldSystemState->GetInternalDependency();
            Dependency = JobHandle.CombineDependencies(this.Dependency, physicsDependency);
        }
    }
}
#endif