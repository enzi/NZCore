using Unity.Entities;
using Unity.Jobs;

namespace NZCore
{
    public static class SystemStateExtensions
    {
        public static void AddSystemWriteDependency<T>(ref this SystemState state)
            where T : unmanaged
        {
            var tmp = TypeManager.GetTypeIndex<T>();
            state.AddReaderWriter(ComponentType.ReadWrite(tmp));
        }
        
        public static void AddSystemWriteDependency(ref this SystemState state, TypeIndex typeIndex)
        {
            state.AddReaderWriter(ComponentType.ReadWrite(typeIndex));
        }
        
        public static void AddSystemReadDependency(ref this SystemState state, TypeIndex typeIndex)
        {
            state.AddReaderWriter(ComponentType.ReadOnly(typeIndex));
        }
        
        public static void AddSystemReadDependency<T>(ref this SystemState state)
            where T : unmanaged
        {
            var tmp = TypeManager.GetTypeIndex<T>();
            state.AddReaderWriter(ComponentType.ReadOnly(tmp));
        }
        
        public static JobHandle GetInternalDependency(ref this SystemState system)
        {
            return system.m_JobHandle;
        }

    }
}