// <copyright project="NZCore" file="PauseSystem.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections;
using Unity.Core;
using Unity.Entities;

namespace NZCore
{
#if !UNITY_NETCODE
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

        protected override void OnUpdate() { }
    }

    public class PauseRateManager : IRateManager
    {
        private EntityQuery _query;
        private readonly bool _isPresentation;

        private bool _isPaused;
        private bool _hasUpdated;
        private double _pauseTime;

        public float Timestep { get; set; }

        public PauseRateManager(ComponentSystemGroup group, bool isPresentation)
        {
            _query = new EntityQueryBuilder(Allocator.Temp)
                     .WithAll<PauseGame>()
                     .WithOptions(EntityQueryOptions.IncludeSystems)
                     .Build(group);

            _isPresentation = isPresentation;
        }

        public bool ShouldGroupUpdate(ComponentSystemGroup group)
        {
            if (_hasUpdated)
            {
                _hasUpdated = false;
                return false;
            }

            var pauses = _query.ToComponentDataArray<PauseGame>(group.WorldUpdateAllocator);

            var shouldPause = false;

            if (_isPresentation)
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

            if (shouldPause && !_isPaused)
            {
                _isPaused = true;
                _pauseTime = group.World.Time.ElapsedTime;
            }
            else if (!shouldPause && _isPaused)
            {
                _isPaused = false;
            }

            if (_isPaused)
            {
                group.World.Time = new TimeData(_pauseTime, group.World.Time.DeltaTime);
                Timestep = 0;
                return false;
            }

            _hasUpdated = true;
            Timestep = group.World.Time.DeltaTime;

            return true;
        }
    }
#endif
}