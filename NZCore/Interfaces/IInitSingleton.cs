// <copyright project="NZCore" file="IInitSingleton.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore.Interfaces
{
    public interface IInitSingleton : IComponentData
    {
        public void Init();
    }
}