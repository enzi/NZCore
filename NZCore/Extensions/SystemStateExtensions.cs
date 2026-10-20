// <copyright project="NZCore" file="SystemStateExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using NZCore.Internal;
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
            EntityInternals.AddReaderWriter(ref state, ComponentType.ReadWrite(tmp));
        }

        public static void AddSystemWriteDependency(ref this SystemState state, TypeIndex typeIndex)
        {
            EntityInternals.AddReaderWriter(ref state, ComponentType.ReadWrite(typeIndex));
        }

        public static void AddSystemReadDependency(ref this SystemState state, TypeIndex typeIndex)
        {
            EntityInternals.AddReaderWriter(ref state, ComponentType.ReadOnly(typeIndex));
        }

        public static void AddSystemReadDependency<T>(ref this SystemState state)
            where T : unmanaged
        {
            var tmp = TypeManager.GetTypeIndex<T>();
            EntityInternals.AddReaderWriter(ref state, ComponentType.ReadOnly(tmp));
        }

        public static JobHandle GetInternalDependency(ref this SystemState state) => state.GetJobHandle();

        [ExcludeFromBurstCompatTesting("Returns managed system")]
        public static ComponentSystemBase GetManagedSystem(ref this SystemState state) => EntityInternals.GetManagedSystem(ref state);

        /// <summary> Name of the profiler marker the system's update is recorded under, managed and unmanaged systems use the same format. </summary>
        [ExcludeFromBurstCompatTesting("Returns managed string")]
        public static string GetSystemProfilerMarkerName(ref this SystemState state, World world) => EntityInternals.GetProfilerMarkerName(ref state, world);

        /// <summary>
        /// Copies the component types the system registered for job dependencies (queries, type handles, lookups).
        /// A type is either in reading or in writing, writes take precedence.
        /// </summary>
        [ExcludeFromBurstCompatTesting("Uses managed lists")]
        public static void GetReaderWriterTypes(ref this SystemState state, List<TypeIndex> reading, List<TypeIndex> writing)
        {
            reading.Clear();
            writing.Clear();

            foreach (var dep in state.GetJobDependencyForReadingSystems())
            {
                reading.Add(dep);
            }

            foreach (var dep in state.GetJobDependencyForWritingSystems())
            {
                writing.Add(dep);
            }
        }

        /// <summary>
        /// Copies all queries of the system and the subset registered through RequireForUpdate.
        /// </summary>
        [ExcludeFromBurstCompatTesting("Uses managed lists")]
        public static void GetEntityQueries(ref this SystemState state, List<EntityQuery> queries, List<EntityQuery> requiredQueries)
        {
            queries.Clear();
            requiredQueries.Clear();

            ref var all = ref EntityInternals.GetEntityQueries(ref state);
            foreach (var query in all)
            {
                queries.Add(query);
            }

            ref var required = ref EntityInternals.GetRequiredEntityQueries(ref state);
            foreach (var query in required)
            {
                requiredQueries.Add(query);
            }
        }

        public static EntityQuery GetSingletonQuery<T>(ref this SystemState state)
            where T : unmanaged =>
            new EntityQueryBuilder(Allocator.Temp)
                .WithAll<T>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build(ref state);

        public static T GetSingleton<T>(ref this SystemState state)
            where T : unmanaged, IComponentData
        {
            var query = state.GetSingletonQuery<T>();
            return query.GetSingleton<T>();
        }

        public static Entity GetSingletonEntity<T>(ref this SystemState state)
            where T : unmanaged
        {
            var query = state.GetSingletonQuery<T>();
            query.CompleteDependency();

            return query.GetSingletonEntity();
        }

        public static DynamicBuffer<T> GetSingletonBuffer<T>(ref this SystemState state)
            where T : unmanaged, IBufferElementData
        {
            var query = state.GetSingletonQuery<T>();
            return query.GetSingletonBuffer<T>();
        }

        public static DynamicBuffer<T> GetSingletonBufferNoSync<T>(ref this SystemState state, bool isReadOnly)
            where T : unmanaged, IBufferElementData
        {
            var query = state.GetSingletonQuery<T>();
            return query.GetSingletonBufferNoSync<T>(isReadOnly);
        }

        public static UntypedBufferLookup GetUntypedBufferLookup(ref this SystemState state, ComponentType componentType, bool isReadOnly)
        {
            EntityInternals.AddReaderWriter(ref state, componentType);
            return state.EntityManager.GetUntypedBufferLookup(componentType, isReadOnly);
        }
    }
}