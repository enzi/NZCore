// <copyright project="NZCore" file="DateTimeSystem.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.Components;
using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class DateTimeSystem : SystemBase
    {
        private const int UPDATE_INTERVAL = 1; // in seconds

        private double lastElapsed;

        protected override void OnCreate()
        {
            CheckedStateRef.CreateSingleton<DateTimeSingleton>();

            SystemAPI.SetSingleton(new DateTimeSingleton()
            {
                UtcNowBinary = DateTime.UtcNow.ToBinary()
            });
        }

        protected override void OnDestroy()
        {
            CheckedStateRef.DisposeSingleton<DateTimeSingleton>();
        }

        protected override void OnUpdate()
        {
            var currentElapsed = SystemAPI.Time.ElapsedTime;

            // only update very second
            if (currentElapsed > lastElapsed + UPDATE_INTERVAL)
            {
                lastElapsed = currentElapsed;

                SystemAPI.SetSingleton(new DateTimeSingleton()
                {
                    UtcNowBinary = DateTime.UtcNow.ToBinary()
                });
            }
        }
    }
}