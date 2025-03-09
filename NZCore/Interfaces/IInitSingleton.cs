// <copyright project="NZCore" file="IInitSingleton.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore.Interfaces
{
    public interface IInitSingleton : IComponentData
    {
        public void Init();
    }
}