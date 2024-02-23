using Unity.Entities;
using Unity.Jobs;

namespace NZCore
{
    public static class SystemStateExtensions
    {
        public static void AddSystemWriteDependency(ref this SystemState state, TypeIndex typeIndex)
        {
            state.AddReaderWriter(ComponentType.ReadWrite(typeIndex));
        }
        
        public static void AddSystemReadDependency(ref this SystemState state, TypeIndex typeIndex)
        {
            state.AddReaderWriter(ComponentType.ReadOnly(typeIndex));
        }
        
        public static JobHandle GetInternalDependency(ref this SystemState system)
        {
            return system.m_JobHandle;
        }

    }
}