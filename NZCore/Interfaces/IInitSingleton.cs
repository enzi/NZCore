// <copyright project="NZCore" file="IInitSingleton.cs" version="1.0.0">
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