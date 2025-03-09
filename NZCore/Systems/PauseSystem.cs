// <copyright project="NZCore" file="PauseSystem.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections;
using Unity.Core;
using Unity.Entities;

namespace NZCore
{
    public partial class PauseSystem : SystemBase
    {
        protected override void OnCreate()
        {
            var simulationGroup = World.GetExistingSystemManaged<SimulationSystemGroup>();
            simulationGroup.RateManager = new PauseRateManager(simulationGroup, false);

            var presentationGroup = World.GetExistingSystemManaged<PresentationSystemGroup>();

            if (presentationGroup != null)
            {
                presentationGroup.RateManager = new PauseRateManager(presentationGroup, true);
            }

            Enabled = false;
        }

        protected override void OnUpdate()
        {
        }
    }
    
    public class PauseRateManager : IRateManager
    {
        private EntityQuery query;
        private bool isPresentation;
        
        private bool isPaused;
        private bool hasUpdated;
        private double pauseTime;
        
        public float Timestep { get; set; }

        public PauseRateManager(ComponentSystemGroup group, bool isPresentation)
        {
            query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PauseGame>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build(group);

            this.isPresentation = isPresentation;
        }
        
        public bool ShouldGroupUpdate(ComponentSystemGroup group)
        {
            if (hasUpdated)
            {
                hasUpdated = false;
                return false;
            }
            
            var pauses = query.ToComponentDataArray<PauseGame>(group.WorldUpdateAllocator);
            
            var shouldPause = false;

            if (isPresentation)
            {
                foreach (var pauseGame in pauses)
                {
                    if (pauseGame.PausePresentation == 1)
                    {
                        shouldPause = true;
                        break;
                    }
                }
            }
            else
            {
                shouldPause = pauses.Length > 0;
            }

            if (shouldPause && !isPaused)
            {
                isPaused = true;
                pauseTime = group.World.Time.ElapsedTime;
            }
            else if (!shouldPause && isPaused)
            {
                isPaused = false;
            }

            if (isPaused)
            {
                group.World.Time = new TimeData(pauseTime, group.World.Time.DeltaTime);
                Timestep = 0;
                return false;
            }

            hasUpdated = true;
            Timestep = group.World.Time.DeltaTime;

            return true;
        }
    }
}