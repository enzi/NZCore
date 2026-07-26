using System;
using NUnit.Framework;
using NZCore;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Transforms;

namespace NZCore.Tests
{
    public class SpatialHashPerformanceTests : EcsTestsFixture
    {
        private const int EntityCount = 1_000_000;
        private const int RayCount = 1_000;

        private SpatialHashPerformanceMemberSystem _memberSystem;
        private SystemHandle _buildSystem;
        private NativeArray<SpatialHashRayInput> _rays;
        private NativeArray<int> _results;
        private NativeReference<ulong> _checksum;
        private int _mapCapacity;
        private int _oversizedCapacity;
        private ulong _expectedChecksum;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            JobsUtility.JobDebuggerEnabled = false;

            _memberSystem = World.CreateSystemManaged<SpatialHashPerformanceMemberSystem>();
            _buildSystem = World.GetOrCreateSystem<SpatialHashBuildSystem>();

            var settings = SpatialHashSettings.Default;
            settings.CellSize = 4f;
            settings.MaxIndexedRadius = 0.5f;
            settings.InitialCapacity = EntityCount;
            Manager.SetComponentData(_buildSystem, settings);

            var archetype = Manager.CreateArchetype(typeof(SpatialHashMember), typeof(LocalToWorld));
            Manager.CreateEntity(archetype, EntityCount);

            _memberSystem.Initialize = true;
            _memberSystem.Update();
            _memberSystem.Initialize = false;

            _rays = new NativeArray<SpatialHashRayInput>(RayCount, Allocator.Persistent);
            _results = new NativeArray<int>(RayCount, Allocator.Persistent);
            _checksum = new NativeReference<ulong>(Allocator.Persistent);
            for (var i = 0; i < RayCount; i++)
            {
                _rays[i] = new SpatialHashRayInput
                {
                    Start = new float3(-1f, 0f, i),
                    End = new float3(1001f, 0f, i),
                    QueryMask = 1
                };
            }

            Rebuild();
            var lookup = Manager.GetComponentData<SpatialHashLookup>(_buildSystem);
            _mapCapacity = lookup.Entries.Capacity;
            _oversizedCapacity = lookup.OversizedEntries.Capacity;
            Raycast();
            _expectedChecksum = _checksum.Value;
        }

        [TearDown]
        public override void TearDown()
        {
            if (_checksum.IsCreated)
                _checksum.Dispose();
            if (_results.IsCreated)
                _results.Dispose();
            if (_rays.IsCreated)
                _rays.Dispose();
            base.TearDown();
        }

        [Test, Explicit("Runs the full 1,000,000-member spatial hash workload")]
        public void WorkloadProducesStableResultsWithoutResizeOrManagedAllocation()
        {
            Assert.That(_expectedChecksum, Is.Not.Zero);

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            MoveAndRebuild();
            Raycast();
            var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(_checksum.Value, Is.EqualTo(_expectedChecksum));
            Assert.That(allocatedAfter - allocatedBefore, Is.Zero);
            AssertStableCapacity();

            var lookup = Manager.GetComponentData<SpatialHashLookup>(_buildSystem);
            Assert.That(lookup.Entries.Count(), Is.EqualTo(EntityCount));
            Assert.That(lookup.OversizedCount, Is.Zero);
        }

        [Test, Performance, Explicit("Runs the full 1,000,000-member spatial hash workload")]
        public void Rebuild_MovingMembers()
        {
            Measure.Method(MoveAndRebuild)
                .WarmupCount(15)
                .MeasurementCount(50)
                .GC()
                .Run();

            AssertStableCapacity();
        }

        [Test, Performance, Explicit("Runs 1,000 raycasts against the 1,000,000-member index")]
        public void RaycastClosest_OneThousandRays()
        {
            Measure.Method(Raycast)
                .WarmupCount(15)
                .MeasurementCount(50)
                .GC()
                .Run();

            Assert.That(_checksum.Value, Is.EqualTo(_expectedChecksum));
        }

        [Test, Performance, Explicit("Runs the full moving rebuild and raycast workload")]
        public void EndToEnd_MovingMembersAndRaycasts()
        {
            Measure.Method(() =>
                {
                    MoveAndRebuild();
                    Raycast();
                })
                .WarmupCount(15)
                .MeasurementCount(50)
                .GC()
                .Run();

            Assert.That(_checksum.Value, Is.EqualTo(_expectedChecksum));
            AssertStableCapacity();
        }

        private void MoveAndRebuild()
        {
            _memberSystem.Update();
            Rebuild();
        }

        private void Rebuild()
        {
            _buildSystem.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
        }

        private void Raycast()
        {
            var lookup = Manager.GetComponentData<SpatialHashLookup>(_buildSystem);
            var raycastHandle = new PerformanceRaycastJob
            {
                Lookup = lookup,
                Rays = _rays,
                Results = _results
            }.ScheduleParallel(_rays.Length, 1, default);

            new ChecksumJob
            {
                Results = _results,
                Checksum = _checksum
            }.Schedule(raycastHandle).Complete();
        }

        private void AssertStableCapacity()
        {
            var lookup = Manager.GetComponentData<SpatialHashLookup>(_buildSystem);
            Assert.That(lookup.Entries.Capacity, Is.EqualTo(_mapCapacity));
            Assert.That(lookup.OversizedEntries.Capacity, Is.EqualTo(_oversizedCapacity));
        }
    }

    [DisableAutoCreation]
    public partial class SpatialHashPerformanceMemberSystem : SystemBase
    {
        public bool Initialize;

        private EntityQuery _members;
        private float _direction = 0.01f;

        protected override void OnCreate()
        {
            _members = GetEntityQuery(
                ComponentType.ReadWrite<SpatialHashMember>(),
                ComponentType.ReadWrite<LocalToWorld>());
        }

        protected override void OnUpdate()
        {
            JobHandle handle;
            if (Initialize)
            {
                handle = new InitializeMembersJob
                {
                    EntityHandle = GetEntityTypeHandle(),
                    MemberHandle = GetComponentTypeHandle<SpatialHashMember>(false),
                    LocalToWorldHandle = GetComponentTypeHandle<LocalToWorld>(false)
                }.ScheduleParallel(_members, default);
            }
            else
            {
                handle = new MoveMembersJob
                {
                    LocalToWorldHandle = GetComponentTypeHandle<LocalToWorld>(false),
                    Delta = _direction
                }.ScheduleParallel(_members, default);
                _direction = -_direction;
            }

            handle.Complete();
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    internal unsafe struct InitializeMembersJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle EntityHandle;
        public ComponentTypeHandle<SpatialHashMember> MemberHandle;
        public ComponentTypeHandle<LocalToWorld> LocalToWorldHandle;

        public void Execute(
            in ArchetypeChunk chunk,
            int unfilteredChunkIndex,
            bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var entities = chunk.GetEntityDataPtrRO(EntityHandle);
            var members = chunk.GetComponentDataPtrRW(ref MemberHandle);
            var transforms = chunk.GetComponentDataPtrRW(ref LocalToWorldHandle);
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out var i))
            {
                var entityIndex = entities[i].Index;
                members[i] = new SpatialHashMember { Radius = 0.2f, CategoryMask = 1 };
                transforms[i].Value = float4x4.Translate(new float3(entityIndex % 1000, 0f, entityIndex / 1000));
            }
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    internal unsafe struct MoveMembersJob : IJobChunk
    {
        public ComponentTypeHandle<LocalToWorld> LocalToWorldHandle;
        public float Delta;

        public void Execute(
            in ArchetypeChunk chunk,
            int unfilteredChunkIndex,
            bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var transforms = chunk.GetComponentDataPtrRW(ref LocalToWorldHandle);
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out var i))
                transforms[i].Value.c3.x += Delta;
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    internal struct PerformanceRaycastJob : IJobFor
    {
        [ReadOnly] public SpatialHashLookup Lookup;
        [ReadOnly] public NativeArray<SpatialHashRayInput> Rays;
        [WriteOnly] public NativeArray<int> Results;

        public void Execute(int index)
        {
            Lookup.RaycastClosest(Rays[index], out var hit);
            Results[index] = hit.Entity.Index;
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    internal struct ChecksumJob : IJob
    {
        [ReadOnly] public NativeArray<int> Results;
        public NativeReference<ulong> Checksum;

        public void Execute()
        {
            ulong checksum = 0;
            for (var i = 0; i < Results.Length; i++)
                checksum = checksum * 397UL + (uint)Results[i];
            Checksum.Value = checksum;
        }
    }
}
