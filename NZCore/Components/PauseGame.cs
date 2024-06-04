using Unity.Entities;

namespace NZCore
{
    public struct PauseGame : IComponentData
    {
        public byte PausePresentation;

        public static void Pause(ref SystemState state, bool pausePresentation = false)
        {
            var isPaused = state.EntityManager.HasComponent<PauseGame>(state.SystemHandle);

            state.EntityManager.AddComponentData(state.SystemHandle, new PauseGame { PausePresentation = pausePresentation.ToByte() });

            if (isPaused)
            {
                return;
            }
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