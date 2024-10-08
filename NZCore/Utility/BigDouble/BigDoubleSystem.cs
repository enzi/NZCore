﻿// <copyright project="NZCore" file="BigDoubleSystem.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct BigDoubleSystem : ISystem
    {
        private BigDouble.PowersOf10 lookup;

        public void OnCreate(ref SystemState state)
        {
            lookup = new BigDouble.PowersOf10();
            lookup.Init();
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            lookup.Dispose();
        }
    }
}