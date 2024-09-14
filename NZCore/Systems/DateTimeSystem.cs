// <copyright project="NZCore.Saving" file="DateTimeSystem.cs" version="1.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.Components;
using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class DateTimeSystem : SystemBase
    {
        protected override void OnCreate()
        {
            CheckedStateRef.CreateSingleton<DateTimeSingleton>();
        }

        protected override void OnDestroy()
        {
            CheckedStateRef.DisposeSingleton<DateTimeSingleton>();
        }

        protected override void OnUpdate()
        {
             SystemAPI.SetSingleton(new DateTimeSingleton()
             {
                 UtcNowBinary = DateTime.UtcNow.ToBinary()
             });
        }
    }
}