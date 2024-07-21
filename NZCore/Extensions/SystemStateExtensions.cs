using Unity.Collections;
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

        public static JobHandle GetInternalDependency(ref this SystemState state)
        {
            return state.m_JobHandle;
        }

        public static T GetSingleton<T>(ref this SystemState state)
            where T : unmanaged, IComponentData
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<T>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build(ref state);

            return query.GetSingleton<T>();
        }

        public static Entity GetSingletonEntity<T>(ref this SystemState state)
            where T : unmanaged, IComponentData
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<T>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build(ref state);

            query.CompleteDependency();

            return query.GetSingletonEntity();
        }

        public static DynamicBuffer<T> GetSingletonBuffer<T>(ref this SystemState state)
            where T : unmanaged, IBufferElementData
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<T>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build(ref state);

            return query.GetSingletonBuffer<T>();
        }
    }
}