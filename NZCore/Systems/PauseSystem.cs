// <copyright project="NZCore" file="PauseSystem.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Core;
using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(EndInitializationEntityCommandBufferSystem))]
    public partial struct PauseSystem : ISystem, ISystemStartStop
    {
        private EntityQuery query;
        private bool hasPresentation;
        private double pauseTime;

        public void OnCreate(ref SystemState state)
        {
            query = SystemAPI.QueryBuilder()
                .WithAll<PauseGame>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build();

            state.RequireForUpdate(query);

            hasPresentation = state.WorldUnmanaged.SystemExists<BeginPresentationEntityCommandBufferSystem>();
        }

        public void OnStartRunning(ref SystemState state)
        {
            pauseTime = SystemAPI.Time.ElapsedTime;
        }

        public void OnStopRunning(ref SystemState state)
        {
            Unpause(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            var pauses = query.ToComponentDataArray<PauseGame>(state.WorldUpdateAllocator);
            var pausePresentation = false;

            foreach (var pauseGame in pauses)
            {
                if (pauseGame.PausePresentation == 1)
                {
                    pausePresentation = true;
                    break;
                }
            }

            Pause(ref state, pausePresentation);

            state.WorldUnmanaged.Time = new TimeData(pauseTime, state.WorldUnmanaged.Time.DeltaTime);
        }

        private void Pause(ref SystemState state, bool pausePresentation)
        {
            ref var simulationSystemGroup = ref state.WorldUnmanaged.GetExistingSystemState<SimulationSystemGroup>();
            simulationSystemGroup.Enabled = false;

            if (pausePresentation)
            {
                ref var presentationSystemGroup = ref state.WorldUnmanaged.GetExistingSystemState<PresentationSystemGroup>();
                presentationSystemGroup.Enabled = false;
            }
        }

        private void Unpause(ref SystemState state)
        {
            ref var simulationSystemGroup = ref state.WorldUnmanaged.GetExistingSystemState<SimulationSystemGroup>();
            simulationSystemGroup.Enabled = true;

            if (hasPresentation)
            {
                ref var presentationSystemGroup = ref state.WorldUnmanaged.GetExistingSystemState<PresentationSystemGroup>();
                presentationSystemGroup.Enabled = true;
            }
        }
    }
}