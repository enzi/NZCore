// <copyright project="NZCore.Tests" file="ArenaBufferMultiTypePerformanceTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Random = Unity.Mathematics.Random;

namespace NZCore.Tests.NativeContainers
{
    // Three element types per mode, all a single int so block sizes match across modes and the only variable
    // is where the allocator puts them. Element types have to sit at namespace scope for the generator.

    public struct MultiPagedA : IArenaBuffer { public int Value; }
    public struct MultiPagedB : IArenaBuffer { public int Value; }
    public struct MultiPagedC : IArenaBuffer { public int Value; }

    [InternalBufferCapacity(8)]
    public struct MultiDynamicA : IBufferElementData { public int Value; }

    [InternalBufferCapacity(8)]
    public struct MultiDynamicB : IBufferElementData { public int Value; }

    [InternalBufferCapacity(8)]
    public struct MultiDynamicC : IBufferElementData { public int Value; }

    /// <summary>
    /// The benchmark the single-type suite cannot express: a system reading <b>several</b> arena buffers of one
    /// entity over a chunk.
    ///
    /// This is what ChunkPaged and SharedChunkPaged were built for, and
    /// <see cref="ArenaBufferPerformanceTests"/> never measures it - every job there reads one element type, so
    /// a chunk touches one arena and the page arrangement barely matters. With three types per entity the modes
    /// diverge structurally:
    ///
    /// <list type="bullet">
    /// <item>Paged - three independent arenas, a page table lookup per entity per type.</item>
    /// <item>ChunkPaged - three arenas, but one page per chunk per type, so three page resolves per chunk.</item>
    /// <item>SharedChunkPaged - one arena and <b>one page per chunk for all three types</b>, laid out as three
    /// adjacent runs. A three type read walks one contiguous region instead of three unrelated ones.</item>
    /// <item>DynamicBuffer - three separate heap blocks per entity, scattered.</item>
    /// </list>
    /// </summary>
    [Category("Performance")]
    public unsafe class ArenaBufferMultiTypePerformanceTests : EcsTestsFixture
    {
        private const int EntityCount = 5000;
        private const int WarmupCount = 5;
        private const int MeasureCount = 20;

        private SystemHandle _reserveSystem;
        private SystemHandle _benchmarkSystem;

        private EntityQuery _pagedQuery;
        private EntityQuery _chunkPagedQuery;
        private EntityQuery _sharedQuery;
        private EntityQuery _dynamicQuery;

        private NativeReference<long> _result;
        private NativeList<IntPtr> _heapNoise;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            ArenaBufferRegistry.ResetAll();

            _reserveSystem = World.CreateSystem<ArenaBufferReserveSystem>();
            _benchmarkSystem = World.CreateSystem<ArenaBenchmarkSystem>();

            _pagedQuery = Manager.CreateEntityQuery(ComponentType.ReadWrite<MultiPagedARef>());
            _dynamicQuery = Manager.CreateEntityQuery(ComponentType.ReadWrite<MultiDynamicA>());

            _result = new NativeReference<long>(Allocator.Persistent);
            _heapNoise = new NativeList<IntPtr>(EntityCount * 3, Allocator.Persistent);
        }

        [TearDown]
        public override void TearDown()
        {
            for (var i = 0; i < _heapNoise.Length; i++)
            {
                UnsafeUtility.Free((void*)_heapNoise[i], Allocator.Persistent);
            }

            _heapNoise.Dispose();
            _result.Dispose();

            base.TearDown();
            ArenaBufferRegistry.ResetAll();
        }

        private ref SystemState BenchmarkState => ref World.Unmanaged.GetExistingSystemState<ArenaBenchmarkSystem>();

        /// <summary>
        /// Long lived allocations interleaved with buffer population, so DynamicBuffer's blocks do not come
        /// back clustered in creation order. Applied identically to every mode.
        /// </summary>
        private void AllocateHeapNoise(ref Random random)
        {
            var bytes = random.NextInt(64, 4096);
            _heapNoise.Add((IntPtr)UnsafeUtility.Malloc(bytes, 16, Allocator.Persistent));
        }

        private static void Shuffle(NativeArray<Entity> entities)
        {
            var random = new Random(0x5F3759DF);

            for (var i = entities.Length - 1; i > 0; i--)
            {
                var j = random.NextInt(0, i + 1);
                var swap = entities[i];
                entities[i] = entities[j];
                entities[j] = swap;
            }
        }

        private NativeArray<Entity> CreatePagedEntities(int elementsPerEntity)
        {
            var entities = new NativeArray<Entity>(EntityCount, Allocator.Persistent);

            for (var i = 0; i < EntityCount; i++)
            {
                var entity = Manager.CreateEntity();
                Manager.AddComponentData(entity, MultiPagedARef.Request(math.max(1, elementsPerEntity)));
                Manager.AddComponentData(entity, MultiPagedBRef.Request(math.max(1, elementsPerEntity)));
                Manager.AddComponentData(entity, MultiPagedCRef.Request(math.max(1, elementsPerEntity)));
                entities[i] = entity;
            }

            _reserveSystem.Update(World.Unmanaged);
            World.Unmanaged.ResetUpdateAllocator();

            var a = new MultiPagedALookup(Manager);
            var b = new MultiPagedBLookup(Manager);
            var c = new MultiPagedCLookup(Manager);
            var noise = new Random(0xC0FFEE);

            for (var i = 0; i < EntityCount; i++)
            {
                var ba = a[entities[i]];
                var bb = b[entities[i]];
                var bc = c[entities[i]];

                for (var e = 0; e < elementsPerEntity; e++)
                {
                    ba.Add(new MultiPagedA { Value = e });
                    bb.Add(new MultiPagedB { Value = e });
                    bc.Add(new MultiPagedC { Value = e });
                }

                AllocateHeapNoise(ref noise);
            }

            Shuffle(entities);
            return entities;
        }

        private NativeArray<Entity> CreateDynamicEntities(int elementsPerEntity)
        {
            var entities = new NativeArray<Entity>(EntityCount, Allocator.Persistent);
            var archetype = Manager.CreateArchetype(typeof(MultiDynamicA), typeof(MultiDynamicB), typeof(MultiDynamicC));
            var noise = new Random(0xC0FFEE);

            for (var i = 0; i < EntityCount; i++)
            {
                var entity = Manager.CreateEntity(archetype);

                var ba = Manager.GetBuffer<MultiDynamicA>(entity);
                var bb = Manager.GetBuffer<MultiDynamicB>(entity);
                var bc = Manager.GetBuffer<MultiDynamicC>(entity);

                for (var e = 0; e < elementsPerEntity; e++)
                {
                    ba.Add(new MultiDynamicA { Value = e });
                    bb.Add(new MultiDynamicB { Value = e });
                    bc.Add(new MultiDynamicC { Value = e });
                }

                entities[i] = entity;
                AllocateHeapNoise(ref noise);
            }

            Shuffle(entities);
            return entities;
        }

        /// <summary>Three types summed, so the expected total is three times the single type figure.</summary>
        private void AssertSum(int elementsPerEntity)
        {
            var perEntity = (long)elementsPerEntity * (elementsPerEntity - 1) / 2;
            Assert.AreEqual(perEntity * EntityCount * 3, _result.Value, "the benchmark did not read what it was supposed to");
        }

        [Test]
        [Performance]
        public void MultiTypeChunkSum_ArenaBuffer([Values(4, 64, 128, 256, 512, 1024)] int elementsPerEntity)
        {
            var entities = CreatePagedEntities(elementsPerEntity);

            var job = new PagedMultiSumJob
            {
                A = new MultiPagedATypeHandle(ref BenchmarkState, true),
                B = new MultiPagedBTypeHandle(ref BenchmarkState, true),
                C = new MultiPagedCTypeHandle(ref BenchmarkState, true),
                Result = _result
            };

            Measure
                .Method(() =>
                {
                    _result.Value = 0;
                    job.A.Update(ref BenchmarkState);
                    job.B.Update(ref BenchmarkState);
                    job.C.Update(ref BenchmarkState);
                    job.Run(_pagedQuery);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .SampleGroup(new SampleGroup($"ArenaBuffer.MultiTypeChunkSum_{elementsPerEntity}", SampleUnit.Microsecond))
                .Run();

            AssertSum(elementsPerEntity);
            entities.Dispose();
        }

        [Test]
        [Performance]
        public void MultiTypeChunkSum_DynamicBuffer([Values(4, 64, 128, 256, 512, 1024)] int elementsPerEntity)
        {
            var entities = CreateDynamicEntities(elementsPerEntity);

            var job = new DynamicMultiSumJob
            {
                A = BenchmarkState.GetBufferTypeHandle<MultiDynamicA>(true),
                B = BenchmarkState.GetBufferTypeHandle<MultiDynamicB>(true),
                C = BenchmarkState.GetBufferTypeHandle<MultiDynamicC>(true),
                Result = _result
            };

            Measure
                .Method(() =>
                {
                    _result.Value = 0;
                    job.A.Update(ref BenchmarkState);
                    job.B.Update(ref BenchmarkState);
                    job.C.Update(ref BenchmarkState);
                    job.Run(_dynamicQuery);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .SampleGroup(new SampleGroup($"DynamicBuffer.MultiTypeChunkSum_{elementsPerEntity}", SampleUnit.Microsecond))
                .Run();

            AssertSum(elementsPerEntity);
            entities.Dispose();
        }

        [Test]
        [Performance]
        public void MultiTypeAddChurn_ArenaBuffer([Values(4, 64, 128, 256, 512, 1024)] int elementsPerEntity)
        {
            var entities = CreatePagedEntities(0);

            var job = new PagedMultiChurnJob
            {
                A = new MultiPagedATypeHandle(ref BenchmarkState),
                B = new MultiPagedBTypeHandle(ref BenchmarkState),
                C = new MultiPagedCTypeHandle(ref BenchmarkState),
                ElementCount = elementsPerEntity
            };

            Measure
                .Method(() =>
                {
                    // Inside the measured region on purpose, and for every mode so the harness stays
                    // symmetric. For the shared arena this is where ReHomeShared restores the one page per
                    // chunk layout after buffers have grown out of it; for the others it is a no-op once
                    // everything is reserved. Warmup absorbs the first re-homes, so the median shows the
                    // settled state and the max shows what a re-home costs.
                    _reserveSystem.Update(World.Unmanaged);
                    World.Unmanaged.ResetUpdateAllocator();

                    job.A.Update(ref BenchmarkState);
                    job.B.Update(ref BenchmarkState);
                    job.C.Update(ref BenchmarkState);
                    job.Run(_pagedQuery);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .SampleGroup(new SampleGroup($"ArenaBuffer.MultiTypeAddChurn_{elementsPerEntity}", SampleUnit.Microsecond))
                .Run();

            entities.Dispose();
        }

        [Test]
        [Performance]
        public void MultiTypeAddChurn_DynamicBuffer([Values(4, 64, 128, 256, 512, 1024)] int elementsPerEntity)
        {
            var entities = CreateDynamicEntities(0);

            var job = new DynamicMultiChurnJob
            {
                A = BenchmarkState.GetBufferTypeHandle<MultiDynamicA>(),
                B = BenchmarkState.GetBufferTypeHandle<MultiDynamicB>(),
                C = BenchmarkState.GetBufferTypeHandle<MultiDynamicC>(),
                ElementCount = elementsPerEntity
            };

            Measure
                .Method(() =>
                {
                    // Inside the measured region on purpose, and for every mode so the harness stays
                    // symmetric. For the shared arena this is where ReHomeShared restores the one page per
                    // chunk layout after buffers have grown out of it; for the others it is a no-op once
                    // everything is reserved. Warmup absorbs the first re-homes, so the median shows the
                    // settled state and the max shows what a re-home costs.
                    _reserveSystem.Update(World.Unmanaged);
                    World.Unmanaged.ResetUpdateAllocator();

                    job.A.Update(ref BenchmarkState);
                    job.B.Update(ref BenchmarkState);
                    job.C.Update(ref BenchmarkState);
                    job.Run(_dynamicQuery);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .SampleGroup(new SampleGroup($"DynamicBuffer.MultiTypeAddChurn_{elementsPerEntity}", SampleUnit.Microsecond))
                .Run();

            entities.Dispose();
        }

        [BurstCompile]
        private struct PagedMultiSumJob : IJobChunk
        {
            [ReadOnly] public MultiPagedATypeHandle A;
            [ReadOnly] public MultiPagedBTypeHandle B;
            [ReadOnly] public MultiPagedCTypeHandle C;
            public NativeReference<long> Result;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var a = A.GetAccessor(chunk);
                var b = B.GetAccessor(chunk);
                var c = C.GetAccessor(chunk);
                long sum = 0;

                for (var i = 0; i < a.Length; i++)
                {
                    var ba = a[i];
                    for (var e = 0; e < ba.Length; e++)
                    {
                        sum += ba[e].Value;
                    }

                    var bb = b[i];
                    for (var e = 0; e < bb.Length; e++)
                    {
                        sum += bb[e].Value;
                    }

                    var bc = c[i];
                    for (var e = 0; e < bc.Length; e++)
                    {
                        sum += bc[e].Value;
                    }
                }

                Result.Value += sum;
            }
        }

        [BurstCompile]
        private struct DynamicMultiSumJob : IJobChunk
        {
            [ReadOnly] public BufferTypeHandle<MultiDynamicA> A;
            [ReadOnly] public BufferTypeHandle<MultiDynamicB> B;
            [ReadOnly] public BufferTypeHandle<MultiDynamicC> C;
            public NativeReference<long> Result;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var a = chunk.GetBufferAccessor(ref A);
                var b = chunk.GetBufferAccessor(ref B);
                var c = chunk.GetBufferAccessor(ref C);
                long sum = 0;

                for (var i = 0; i < a.Length; i++)
                {
                    var ba = a[i];
                    for (var e = 0; e < ba.Length; e++)
                    {
                        sum += ba[e].Value;
                    }

                    var bb = b[i];
                    for (var e = 0; e < bb.Length; e++)
                    {
                        sum += bb[e].Value;
                    }

                    var bc = c[i];
                    for (var e = 0; e < bc.Length; e++)
                    {
                        sum += bc[e].Value;
                    }
                }

                Result.Value += sum;
            }
        }

        [BurstCompile]
        private struct PagedMultiChurnJob : IJobChunk
        {
            public MultiPagedATypeHandle A;
            public MultiPagedBTypeHandle B;
            public MultiPagedCTypeHandle C;
            public int ElementCount;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var a = A.GetAccessor(chunk);
                var b = B.GetAccessor(chunk);
                var c = C.GetAccessor(chunk);

                for (var i = 0; i < a.Length; i++)
                {
                    var ba = a[i];
                    ba.Clear();
                    for (var e = 0; e < ElementCount; e++)
                    {
                        ba.Add(new MultiPagedA { Value = e });
                    }

                    var bb = b[i];
                    bb.Clear();
                    for (var e = 0; e < ElementCount; e++)
                    {
                        bb.Add(new MultiPagedB { Value = e });
                    }

                    var bc = c[i];
                    bc.Clear();
                    for (var e = 0; e < ElementCount; e++)
                    {
                        bc.Add(new MultiPagedC { Value = e });
                    }
                }
            }
        }

        [BurstCompile]
        private struct DynamicMultiChurnJob : IJobChunk
        {
            public BufferTypeHandle<MultiDynamicA> A;
            public BufferTypeHandle<MultiDynamicB> B;
            public BufferTypeHandle<MultiDynamicC> C;
            public int ElementCount;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var a = chunk.GetBufferAccessor(ref A);
                var b = chunk.GetBufferAccessor(ref B);
                var c = chunk.GetBufferAccessor(ref C);

                for (var i = 0; i < a.Length; i++)
                {
                    var ba = a[i];
                    ba.Clear();
                    for (var e = 0; e < ElementCount; e++)
                    {
                        ba.Add(new MultiDynamicA { Value = e });
                    }

                    var bb = b[i];
                    bb.Clear();
                    for (var e = 0; e < ElementCount; e++)
                    {
                        bb.Add(new MultiDynamicB { Value = e });
                    }

                    var bc = c[i];
                    bc.Clear();
                    for (var e = 0; e < ElementCount; e++)
                    {
                        bc.Add(new MultiDynamicC { Value = e });
                    }
                }
            }
        }

    }
}
