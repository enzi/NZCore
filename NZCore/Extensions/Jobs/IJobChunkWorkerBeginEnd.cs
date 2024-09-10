// <copyright project="NZCore" file="IJobChunkWorkerBeginEnd.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace NZCore.Jobs
{
    [JobProducerType(typeof(JobChunkWorkerExtensions.JobChunkWorkerProducer<>))]
    public interface IJobChunkWorkerBeginEnd
    {
        void OnWorkerBegin()
        {
        }

        void OnWorkerEnd()
        {
        }

        void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask);
    }

    /// <summary>
    /// Extensions for scheduling and running <see cref="IJobChunkWorkerBeginEnd"/> jobs.
    /// </summary>
    public static class JobChunkWorkerExtensions
    {
        internal unsafe struct JobChunkWorkerWrapper<T> where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#pragma warning disable 414
            [ReadOnly] public EntityQuerySafetyHandles safety;
#pragma warning restore
            // Only used for JobsUtility.PatchBufferMinMaxRanges; the user must also pass this array into the job struct
            // T if they need these indices inside their Execute() implementation. If null, this array will
            // be ignored.
            [NativeDisableUnsafePtrRestriction] [ReadOnly]
            public int* ChunkBaseEntityIndices;
#endif
            public T JobData;

            public UnsafeMatchingArchetypePtrList MatchingArchetypes;
            public UnsafeCachedChunkList CachedChunks;
            public EntityQueryFilter Filter;

            public int IsParallel;
            public int QueryHasEnableableComponents;
        }

        /// <summary>
        /// Gathers and caches reflection data for the internal job system's managed bindings. Unity is responsible for calling this method - don't call it yourself.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <remarks>
        /// When the Jobs package is included in the project, Unity generates code to call EarlyJobInit at startup. This allows Burst compiled code to schedule jobs because the reflection part of initialization, which is not compatible with burst compiler constraints, has already happened in EarlyJobInit.
        ///
        /// __Note__: While the Jobs package code generator handles this automatically for all closed job types, you must register those with generic arguments (like IJobChunkWorkerBeginEnd&amp;lt;MyJobType&amp;lt;T&amp;gt;&amp;gt;) manually for each specialization with [[Unity.Jobs.RegisterGenericJobTypeAttribute]].
        /// </remarks>
        public static void EarlyJobInit<T>()
            where T : struct, IJobChunkWorkerBeginEnd
        {
            JobChunkWorkerProducer<T>.Initialize();
        }

        /// <summary>
        /// Adds an <see cref="IJobChunkWorkerBeginEnd"/> instance to the job scheduler queue for sequential (non-parallel) execution.
        /// </summary>
        /// <param name="jobData">An <see cref="IJobChunkWorkerBeginEnd"/> instance.</param>
        /// <param name="query">The query selecting chunks with the necessary components.</param>
        /// <param name="dependsOn">The handle identifying already scheduled jobs that must complete before this job is executed.
        /// For example, a job that writes to a component cannot run in parallel with other jobs that read or write that component.
        /// Jobs that only read the same components can run in parallel.
        ///
        /// Most frequently, an appropriate value for this parameter is <see cref="SystemState.Dependency"/> to ensure
        /// that jobs registered with the safety system are taken into account as input dependencies.</param>
        /// <typeparam name="T">The specific <see cref="IJobChunkWorkerBeginEnd"/> implementation type.</typeparam>
        /// <returns>A handle that combines the current Job with previous dependencies identified by the <paramref name="dependsOn"/>
        /// parameter.</returns>
        public static JobHandle Schedule<T>(
            this T jobData,
            EntityQuery query,
            JobHandle dependsOn)
            where T : struct, IJobChunkWorkerBeginEnd
        {
            return ScheduleInternal(ref jobData, query, dependsOn, ScheduleMode.Single, default(NativeArray<int>));
        }

        /// <summary>
        /// Adds an <see cref="IJobChunkWorkerBeginEnd"/> instance to the job scheduler queue for sequential (non-parallel) execution.
        /// </summary>
        /// <param name="jobData">An <see cref="IJobChunkWorkerBeginEnd"/> instance. In this variant, the jobData is passed by
        /// reference, which may be necessary for unusually large job structs.</param>
        /// <param name="query">The query selecting chunks with the necessary components.</param>
        /// <param name="dependsOn">The handle identifying already scheduled jobs that must complete before this job is executed.
        /// For example, a job that writes to a component cannot run in parallel with other jobs that read or write that component.
        /// Jobs that only read the same components can run in parallel.
        ///
        /// Most frequently, an appropriate value for this parameter is <see cref="SystemState.Dependency"/> to ensure
        /// that jobs registered with the safety system are taken into account as input dependencies.</param>
        /// <typeparam name="T">The specific <see cref="IJobChunkWorkerBeginEnd"/> implementation type.</typeparam>
        /// <returns>A handle that combines the current Job with previous dependencies identified by the <paramref name="dependsOn"/>
        /// parameter.</returns>
        public static JobHandle ScheduleByRef<T>(
            this ref T jobData,
            EntityQuery query,
            JobHandle dependsOn)
            where T : struct, IJobChunkWorkerBeginEnd
        {
            return ScheduleInternal(ref jobData, query, dependsOn, ScheduleMode.Single, default(NativeArray<int>));
        }

        /// <summary>
        /// Adds an <see cref="IJobChunkWorkerBeginEnd"/> instance to the job scheduler queue for parallel execution.
        /// </summary>
        /// <param name="jobData">An <see cref="IJobChunkWorkerBeginEnd"/> instance.</param>
        /// <param name="query">The query selecting chunks with the necessary components.</param>
        /// <param name="dependsOn">The handle identifying already scheduled jobs that must complete before this job is executed.
        /// For example, a job that writes to a component cannot run in parallel with other jobs that read or write that component.
        /// Jobs that only read the same components can run in parallel.
        ///
        /// Most frequently, an appropriate value for this parameter is <see cref="SystemState.Dependency"/> to ensure
        /// that jobs registered with the safety system are taken into account as input dependencies.</param>
        /// <typeparam name="T">The specific <see cref="IJobChunkWorkerBeginEnd"/> implementation type.</typeparam>
        /// <returns>A handle that combines the current Job with previous dependencies identified by the <paramref name="dependsOn"/>
        /// parameter.</returns>
        public static JobHandle ScheduleParallel<T>(
            this T jobData,
            EntityQuery query,
            JobHandle dependsOn)
            where T : struct, IJobChunkWorkerBeginEnd
        {
            return ScheduleInternal(ref jobData, query, dependsOn, ScheduleMode.Parallel, default(NativeArray<int>));
        }

        /// <summary>
        /// Adds an <see cref="IJobChunkWorkerBeginEnd"/> instance to the job scheduler queue for parallel execution.
        /// </summary>
        /// <param name="jobData">An <see cref="IJobChunkWorkerBeginEnd"/> instance. In this variant, the jobData is passed by
        /// reference, which may be necessary for unusually large job structs.</param>
        /// <param name="query">The query selecting chunks with the necessary components.</param>
        /// <param name="dependsOn">The handle identifying already scheduled jobs that must complete before this job is executed.
        /// For example, a job that writes to a component cannot run in parallel with other jobs that read or write that component.
        /// Jobs that only read the same components can run in parallel.
        ///
        /// Most frequently, an appropriate value for this parameter is <see cref="SystemState.Dependency"/> to ensure
        /// that jobs registered with the safety system are taken into account as input dependencies.</param>
        /// <typeparam name="T">The specific <see cref="IJobChunkWorkerBeginEnd"/> implementation type.</typeparam>
        /// <returns>A handle that combines the current Job with previous dependencies identified by the <paramref name="dependsOn"/>
        /// parameter.</returns>
        public static JobHandle ScheduleParallelByRef<T>(
            this ref T jobData,
            EntityQuery query,
            JobHandle dependsOn)
            where T : struct, IJobChunkWorkerBeginEnd
        {
            return ScheduleInternal(ref jobData, query, dependsOn, ScheduleMode.Parallel, default(NativeArray<int>));
        }

        /// <summary>
        /// Runs the job immediately on the current thread.
        /// </summary>
        /// <param name="jobData">An <see cref="IJobChunkWorkerBeginEnd"/> instance.</param>
        /// <param name="query">The query selecting chunks with the necessary components.</param>
        /// <typeparam name="T">The specific <see cref="IJobChunkWorkerBeginEnd"/> implementation type.</typeparam>
        public static void Run<T>(this T jobData, EntityQuery query)
            where T : struct, IJobChunkWorkerBeginEnd
        {
            ScheduleInternal(ref jobData, query, default(JobHandle), ScheduleMode.Run, default(NativeArray<int>));
        }

        /// <summary>
        /// Runs the job immediately on the current thread.
        /// </summary>
        /// <param name="jobData">An <see cref="IJobChunkWorkerBeginEnd"/> instance. In this variant, the jobData is passed by
        /// reference, which may be necessary for unusually large job structs.</param>
        /// <param name="query">The query selecting chunks with the necessary components.</param>
        /// <typeparam name="T">The specific <see cref="IJobChunkWorkerBeginEnd"/> implementation type.</typeparam>
        public static void RunByRef<T>(this ref T jobData, EntityQuery query)
            where T : struct, IJobChunkWorkerBeginEnd
        {
            ScheduleInternal(ref jobData, query, default(JobHandle), ScheduleMode.Run, default(NativeArray<int>));
        }

        internal static unsafe void RunByRefWithoutJobs<T>(this ref T jobData, EntityQuery query)
            where T : struct, IJobChunkWorkerBeginEnd
        {
            var queryImpl = query._GetImpl();
            if (queryImpl->_Access->IsInExclusiveTransaction)
                throw new InvalidOperationException("You can't schedule an IJobChunkWorkerBeginEnd while an exclusive transaction is active");
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            queryImpl->_Access->DependencyManager->ForEachStructuralChange.BeginIsInForEach(queryImpl);
#endif
            if (query.HasFilter() || queryImpl->_QueryData->HasEnableableComponents != 0)
            {
                // Complete any running jobs that would affect which chunks/entities match the query.
                // This sync may not be strictly necessary, if the caller doesn't care about filtering the query results.
                // But if they DO care, and they forget this sync, they'll have an undetected race condition. So, let's play it safe.
                queryImpl->SyncFilterTypes();

                var chunkCacheIterator = new UnsafeChunkCacheIterator(queryImpl->_Filter, queryImpl->_QueryData->HasEnableableComponents != 0,
                    queryImpl->GetMatchingChunkCache(), queryImpl->_QueryData->MatchingArchetypes.Ptr);

                int chunkIndex = -1;
                v128 chunkEnabledMask = default;
                while (chunkCacheIterator.MoveNextChunk(ref chunkIndex, out var archetypeChunk, out _,
                           out byte useEnabledMask, ref chunkEnabledMask))
                {
                    jobData.Execute(archetypeChunk, chunkIndex, useEnabledMask != 0, chunkEnabledMask);
                }
            }
            else
            {
                // Fast path for queries with no filtering and no enableable components
                var cachedChunkList = queryImpl->GetMatchingChunkCache();
                var chunkIndices = cachedChunkList.ChunkIndices;
                int chunkCount = cachedChunkList.Length;
                ArchetypeChunk chunk = new ArchetypeChunk(ChunkIndex.Null, cachedChunkList.EntityComponentStore);
                v128 defaultMask = default;
                for (int chunkIndex = 0; chunkIndex < chunkCount; ++chunkIndex)
                {
                    chunk.m_Chunk = chunkIndices[chunkIndex];
                    //Assert.AreNotEqual(0, chunk.Count);
                    jobData.Execute(chunk, chunkIndex, false, defaultMask);
                }
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            queryImpl->_Access->DependencyManager->ForEachStructuralChange.EndIsInForEach();
#endif
        }

        internal static unsafe JobHandle ScheduleInternal<T>(
            ref T jobData,
            EntityQuery query,
            JobHandle dependsOn,
            ScheduleMode mode,
            NativeArray<int> chunkBaseEntityIndices)
            where T : struct, IJobChunkWorkerBeginEnd
        {
            var queryImpl = query._GetImpl();
            if (queryImpl->_Access->IsInExclusiveTransaction)
                throw new InvalidOperationException("You can't schedule an IJobChunkWorkerBeginEnd while an exclusive transaction is active");
            var queryData = queryImpl->_QueryData;
            var cachedChunks = queryImpl->GetMatchingChunkCache();
            var totalChunkCount = cachedChunks.Length;
            bool isParallel = (mode == ScheduleMode.Parallel);

            JobChunkWorkerWrapper<T> jobChunkWrapper = new JobChunkWorkerWrapper<T>
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                safety = new EntityQuerySafetyHandles(queryImpl),
                // If this array exists, it's likely still being written to by a job, so we need to bypass the safety system to get its buffer pointer
                ChunkBaseEntityIndices = (isParallel && chunkBaseEntityIndices.Length > 0)
                    ? (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(chunkBaseEntityIndices)
                    : null,
#endif
                MatchingArchetypes = queryData->MatchingArchetypes,
                CachedChunks = cachedChunks,
                Filter = queryImpl->_Filter,

                JobData = jobData,
                IsParallel = isParallel ? 1 : 0,

                QueryHasEnableableComponents = queryData->HasEnableableComponents != 0 ? 1 : 0
            };
            JobChunkWorkerProducer<T>.Initialize();
            var reflectionData = JobChunkWorkerProducer<T>.reflectionData.Data;
            CollectionHelper.CheckReflectionDataCorrect<T>(reflectionData);

            var scheduleParams = new JobsUtility.JobScheduleParameters(
                UnsafeUtility.AddressOf(ref jobChunkWrapper),
                reflectionData,
                dependsOn,
                mode);

            JobHandle result;
            if (!isParallel)
            {
                result = JobsUtility.Schedule(ref scheduleParams);
            }
            else
            {
                // TODO(DOTS-5740): pick a better innerloopBatchCount
                result = JobsUtility.ScheduleParallelFor(ref scheduleParams, totalChunkCount, 1);
            }

            return result;
        }

        internal struct JobChunkWorkerProducer<T>
            where T : struct, IJobChunkWorkerBeginEnd
        {
            internal static readonly SharedStatic<IntPtr> reflectionData = SharedStatic<IntPtr>.GetOrCreate<JobChunkWorkerProducer<T>>();

            [BurstDiscard]
            internal static void Initialize()
            {
                if (reflectionData.Data == IntPtr.Zero)
                    reflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobChunkWorkerWrapper<T>), typeof(T), (ExecuteJobFunction)Execute);
            }

            public delegate void ExecuteJobFunction(
                ref JobChunkWorkerWrapper<T> jobWrapper,
                IntPtr additionalPtr,
                IntPtr bufferRangePatchData,
                ref JobRanges ranges,
                int jobIndex);

            public static void Execute(
                ref JobChunkWorkerWrapper<T> jobWrapper,
                IntPtr additionalPtr,
                IntPtr bufferRangePatchData,
                ref JobRanges ranges,
                int jobIndex)
            {
                ExecuteInternal(ref jobWrapper, bufferRangePatchData, ref ranges, jobIndex);
            }

            internal unsafe static void ExecuteInternal(
                ref JobChunkWorkerWrapper<T> jobWrapper,
                IntPtr bufferRangePatchData,
                ref JobRanges ranges,
                int jobIndex)
            {
                var chunks = jobWrapper.CachedChunks;
                var chunkCacheIterator = new UnsafeChunkCacheIterator(jobWrapper.Filter,
                    jobWrapper.QueryHasEnableableComponents != 0,
                    jobWrapper.CachedChunks, jobWrapper.MatchingArchetypes.Ptr);

                bool isParallel = jobWrapper.IsParallel == 1;
                bool isFiltering = jobWrapper.Filter.RequiresMatchesFilter;
                bool executed = false;

                while (true)
                {
                    int beginChunkIndex = 0;
                    int endChunkIndex = chunks.Length;

                    // If we are running the job in parallel, steal some work.
                    if (isParallel)
                    {
                        // If we have no range to steal, exit the loop.
                        if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out beginChunkIndex, out endChunkIndex))
                        {
                            if (executed)
                            {
                                jobWrapper.JobData.OnWorkerEnd();
                            }

                            break;
                        }
                    }

                    if (!executed)
                    {
                        executed = true;
                        jobWrapper.JobData.OnWorkerBegin();
                    }

                    // Do the actual user work.
                    if (jobWrapper.QueryHasEnableableComponents == 0 && !isFiltering)
                    {
                        // Fast path with no entity/chunk filtering active: we can just iterate over the cached chunk list directly.
                        var chunkIndices = chunks.ChunkIndices;
                        ArchetypeChunk chunk = new ArchetypeChunk(ChunkIndex.Null, chunks.EntityComponentStore);
                        v128 defaultMask = default;
                        for (int chunkIndex = beginChunkIndex; chunkIndex < endChunkIndex; ++chunkIndex)
                        {
                            chunk.m_Chunk = chunkIndices[chunkIndex];
                            //Assert.AreNotEqual(0, chunk.Count);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                            // For containers passed in the job struct, parallel jobs can limit writes to a range of indices.
                            // By default, writes are limited to the element corresponding to the unfiltered chunk index.
                            // If the user has provided a ChunkBaseEntityIndices array, use that instead (limiting writes to the range of entities within the chunk).
                            // This range check can be disabled on a per-container basis by adding [NativeDisableParallelForRestriction] to the container field in the job struct.
                            if (Hint.Unlikely(jobWrapper.ChunkBaseEntityIndices != null))
                            {
                                int chunkBaseEntityIndex = jobWrapper.ChunkBaseEntityIndices[chunkIndex];
                                JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData,
                                    UnsafeUtility.AddressOf(ref jobWrapper), chunkBaseEntityIndex, chunk.Count);
                            }
                            else if (isParallel)
                            {
                                JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData,
                                    UnsafeUtility.AddressOf(ref jobWrapper), chunkIndex, 1);
                            }
#endif
                            jobWrapper.JobData.Execute(chunk, chunkIndex, false, defaultMask);
                        }
                    }
                    else
                    {
                        // With any filtering active, we need to iterate using the UnsafeChunkCacheIterator.
                        // Update cache range
                        chunkCacheIterator.Length = endChunkIndex;
                        int chunkIndex = beginChunkIndex - 1;

                        v128 chunkEnabledMask = default;
                        while (chunkCacheIterator.MoveNextChunk(ref chunkIndex, out var chunk, out var chunkEntityCount,
                                   out byte useEnabledMask, ref chunkEnabledMask))
                        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                            // For containers passed in the job struct, parallel jobs can limit writes to a range of indices.
                            // By default, writes are limited to the element corresponding to the unfiltered chunk index.
                            // If the user has provided a ChunkBaseEntityIndices array, use that instead (limiting writes to the range of entities within the chunk).
                            // This range check can be disabled on a per-container basis by adding [NativeDisableParallelForRestriction] to the container field in the job struct.
                            if (Hint.Unlikely(jobWrapper.ChunkBaseEntityIndices != null))
                            {
                                int chunkBaseEntityIndex = jobWrapper.ChunkBaseEntityIndices[chunkIndex];
                                JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData,
                                    UnsafeUtility.AddressOf(ref jobWrapper), chunkBaseEntityIndex, chunk.Count);
                            }
                            else if (isParallel)
                            {
                                JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData,
                                    UnsafeUtility.AddressOf(ref jobWrapper), chunkIndex, 1);
                            }
#endif
                            jobWrapper.JobData.Execute(chunk, chunkIndex, useEnabledMask != 0,
                                chunkEnabledMask);
                        }
                    }

                    // If we are not running in parallel, our job is done.
                    if (!isParallel)
                    {
                        jobWrapper.JobData.OnWorkerEnd();
                        break;
                    }
                }
            }
        }
    }
}