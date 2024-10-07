// <copyright project="NZCore" file="ISavable.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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