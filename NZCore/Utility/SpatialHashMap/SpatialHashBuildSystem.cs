using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace NZCore
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial struct SpatialHashBuildSystem : ISystem
    {
        private EntityQuery _members;

        public void OnCreate(ref SystemState state)
        {
            _members = SystemAPI.QueryBuilder()
                .WithAll<SpatialHashMember, LocalToWorld>()
                .Build();

            var settings = SpatialHashSettings.Default;
            state.EntityManager.AddComponentData(state.SystemHandle, settings);
            state.EntityManager.AddComponentData(
                state.SystemHandle,
                SpatialHashLookup.Create(settings, Allocator.Persistent));
        }

        public void OnDestroy(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<SpatialHashLookup>())
            {
                var lookup = SystemAPI.GetSingleton<SpatialHashLookup>();
                state.Dependency.Complete();
                lookup.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            var settings = SystemAPI.GetSingleton<SpatialHashSettings>();
            ref var lookup = ref SystemAPI.GetSingletonRW<SpatialHashLookup>().ValueRW;
            lookup.Settings = settings;

            if (!settings.IsValid)
            {
                lookup.Valid = 0;
                state.Dependency = new ClearSpatialHashJob
                {
                    Entries = lookup.Entries,
                    OversizedEntries = lookup.OversizedEntries
                }.Schedule(state.Dependency);
                return;
            }

            var entityCount = _members.CalculateEntityCount();
            var requiredCapacity = math.max(settings.InitialCapacity, entityCount);
            if (lookup.Entries.Capacity < requiredCapacity || lookup.OversizedEntries.Capacity < requiredCapacity)
            {
                state.Dependency.Complete();
                if (lookup.Entries.Capacity < requiredCapacity)
                    lookup.Entries.Capacity = requiredCapacity;
                if (lookup.OversizedEntries.Capacity < requiredCapacity)
                    lookup.OversizedEntries.Capacity = requiredCapacity;
            }

            var clearHandle = new ClearSpatialHashJob
            {
                Entries = lookup.Entries,
                OversizedEntries = lookup.OversizedEntries
            }.Schedule(state.Dependency);

            state.Dependency = new BuildSpatialHashJob
            {
                EntityHandle = SystemAPI.GetEntityTypeHandle(),
                MemberHandle = SystemAPI.GetComponentTypeHandle<SpatialHashMember>(true),
                LocalToWorldHandle = SystemAPI.GetComponentTypeHandle<LocalToWorld>(true),
                CellSize = settings.CellSize,
                MaxIndexedRadius = settings.MaxIndexedRadius,
                Entries = lookup.Entries.AsParallelWriter(),
                OversizedEntries = lookup.OversizedEntries.AsParallelWriter()
            }.ScheduleParallel(_members, clearHandle);

            lookup.Valid = 1;
        }

        [BurstCompile]
        private struct ClearSpatialHashJob : IJob
        {
            public NativeParallelMultiHashMap<int2, SpatialHashEntry> Entries;
            public NativeList<SpatialHashEntry> OversizedEntries;

            public void Execute()
            {
                Entries.Clear();
                OversizedEntries.Clear();
            }
        }

        [BurstCompile]
        private unsafe struct BuildSpatialHashJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<SpatialHashMember> MemberHandle;
            [ReadOnly] public ComponentTypeHandle<LocalToWorld> LocalToWorldHandle;
            public float CellSize;
            public float MaxIndexedRadius;
            public NativeParallelMultiHashMap<int2, SpatialHashEntry>.ParallelWriter Entries;
            public NativeList<SpatialHashEntry>.ParallelWriter OversizedEntries;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetEntityDataPtrRO(EntityHandle);
                var members = chunk.GetComponentDataPtrRO(ref MemberHandle);
                var transforms = chunk.GetComponentDataPtrRO(ref LocalToWorldHandle);
                var keys = stackalloc int2[TypeManager.MaximumChunkCapacity];
                var values = stackalloc SpatialHashEntry[TypeManager.MaximumChunkCapacity];
                var normalCount = 0;
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var i))
                {
                    var member = members[i];
                    var position = transforms[i].Position;
                    if (member.CategoryMask == 0 || member.Radius < 0f || !math.isfinite(member.Radius) ||
                        !SpatialHashUtility.TryGetCell(position, CellSize, out var cell))
                        continue;

                    var entry = new SpatialHashEntry
                    {
                        Entity = entities[i],
                        Position = position,
                        Radius = member.Radius,
                        CategoryMask = member.CategoryMask
                    };

                    if (member.Radius > MaxIndexedRadius)
                    {
                        OversizedEntries.AddNoResize(entry);
                        continue;
                    }

                    keys[normalCount] = cell;
                    values[normalCount] = entry;
                    normalCount++;
                }

                if (normalCount > 0)
                    Entries.AddBatchUnsafe(keys, values, normalCount);
            }
        }
    }
}
