// <copyright project="NZCore" file="ISavable.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore
{
    public interface ISavable
    {
    }

    public interface ISavableObject : IDisposable
    {
        void Init();
    }
}