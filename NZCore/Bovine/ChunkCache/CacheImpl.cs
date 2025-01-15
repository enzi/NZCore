// <copyright file="CacheImpl.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    public unsafe struct CacheImpl<T, TC, TCC>
        where TCC : unmanaged,  ICacheComponent<TC>
        where TC : unmanaged, IEntityCache
    {
        private EntityQuery query;
        private EntityQuery missingQuery;
        private EntityTypeHandle entityHandle;
        private ComponentTypeHandle<TCC> cacheHandle;
        private NativeQueue<Ptr<UnsafeList<TC>>> lists;

        public void OnCreate(ref SystemState state)
        {
            lists = new NativeQueue<Ptr<UnsafeList<TC>>>(Allocator.Persistent);

            using var builder = new EntityQueryBuilder(Allocator.Temp);

            query = builder.WithAllChunkComponentRW<TCC>().Build(ref state);
            query.AddOrderVersionFilter();

            // By ensuring prefabs are given the chunk component we avoid entities instantiated ending up in individual chunks
            builder.Reset();
            missingQuery = builder.WithAll<T>().WithNoneChunkComponent<TCC>().WithOptions(EntityQueryOptions.IncludePrefab).Build(ref state);

            entityHandle = state.GetEntityTypeHandle();
            cacheHandle = state.GetComponentTypeHandle<TCC>();
        }

        public void OnDestroy()
        {
            while (lists.TryDequeue(out var list))
            {
                UnsafeList<TC>.Destroy(list.Value);
            }

            lists.Dispose();
        }

        public void OnUpdate(ref SystemState state, UpdateCacheJob job = default)
        {
            state.EntityManager.AddComponent(missingQuery, ComponentType.ChunkComponent<TCC>());

            entityHandle.Update(ref state);
            cacheHandle.Update(ref state);

            job.EntityHandle = entityHandle;
            job.CacheHandle = cacheHandle;
            job.Lists = lists.AsParallelWriter();
            state.Dependency = job.ScheduleParallel(query, state.Dependency);
        }

        [BurstCompile]
        public struct UpdateCacheJob : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle EntityHandle;

            public ComponentTypeHandle<TCC> CacheHandle;

            public NativeQueue<Ptr<UnsafeList<TC>>>.ParallelWriter Lists;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                ref var cacheComponent = ref chunk.GetChunkComponentDataRW(ref CacheHandle);

                if (cacheComponent.Cache == null)
                {
                    cacheComponent.Cache = UnsafeList<TC>.Create(chunk.Capacity, Allocator.Persistent);
                    Lists.Enqueue(cacheComponent.Cache);
                }

                var cache = *cacheComponent.Cache;

                var entities = chunk.GetEntityDataPtrRO(EntityHandle);

                for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                {
                    // At end of cache but still have entities to go, just populate
                    if (entityIndex == cache.Length)
                    {
                        for (; entityIndex < chunk.Count; entityIndex++)
                        {
                            cache.Add(new TC { Entity = entities[entityIndex] });
                        }

                        break;
                    }

                    // Already in place
                    if (cache.Ptr[entityIndex].Entity.Equals(entities[entityIndex]))
                    {
                        continue;
                    }

                    // Order is messed up, try find existing data
                    int cacheIndex;
                    for (cacheIndex = entityIndex + 1; cacheIndex < cache.Length; cacheIndex++)
                    {
                        if (cache.Ptr[cacheIndex].Entity.Equals(entities[entityIndex]))
                        {
                            break;
                        }
                    }

                    // Wasn't found, just add it to end and we'll swap it in
                    if (cacheIndex == cache.Length)
                    {
                        cache.Add(new TC { Entity = entities[entityIndex] });
                    }

                    // Swap
                    (cache[entityIndex], cache[cacheIndex]) = (cache[cacheIndex], cache[entityIndex]);
                }

                // Shrink cache if we need
                cache.Resize(chunk.Count);
                cache.TrimExcess();
                *cacheComponent.Cache = cache;
            }
        }
    }
}
