// <copyright project="NZCore" file="PauseGame.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    public struct PauseGame : IComponentData
    {
        public byte PausePresentation;

        public static void Pause(ref SystemState state, bool pausePresentation = false)
        {
            state.EntityManager.AddComponentData(state.SystemHandle, new PauseGame { PausePresentation = pausePresentation.ToByte() });
        }

        public static void Unpause(ref SystemState state)
        {
            if (!state.EntityManager.HasComponent<PauseGame>(state.SystemHandle))
            {
                return;
            }

            state.EntityManager.RemoveComponent<PauseGame>(state.SystemHandle);
        }
    }
}