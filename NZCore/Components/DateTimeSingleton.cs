// <copyright project="NZCore" file="DateTimeSingleton.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.Interfaces;

namespace NZCore.Components
{
    public struct DateTimeSingleton : IInitSingleton, IDisposable
    {
        public long UtcNowBinary;

        public void Init()
        {
        }

        public void Dispose()
        {
        }
    }
}