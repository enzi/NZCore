// <copyright project="NZCore.Tests" file="ArenaBufferPerformanceTests.cs">
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
    /// <summary>
    /// Empty system whose SystemState the benchmarks borrow to build type handles and lookups.
    /// </summary>
    [DisableAutoCreation]
    public partial struct ArenaBenchmarkSystem : ISystem
    {
    }

    /// <summary>
    /// ArenaBuffer against DynamicBuffer at element counts either side of DynamicTestElement's
    /// InternalBufferCapacity of 8.
    ///
    /// Both sides run the same shape of code so the numbers say something about the containers rather than
    /// about the API used to reach them: work happens inside a <see cref="BurstCompileAttribute"/> job,
    /// handles and lookups are built once outside the measured region, and nothing calls into EntityManager
    /// while measuring. An earlier version of these tests called Manager.GetBuffer per entity inside the
    /// loop - which completes job dependencies and acquires two safety handles every call - against a
    /// prebuilt lookup on the arena side, and so mostly measured that asymmetry.
    ///
    /// Chunk iteration is the interesting pair: it removes the per entity lookup entirely and leaves memory
    /// layout as the main difference.
    /// </summary>
    [Category("Performance")]
    public unsafe class ArenaBufferPerformanceTests : EcsTestsFixture
    {
        private const int EntityCount = 50_000;
        private const int WarmupCount = 5;
        private const int MeasureCount = 20;

        private SystemHandle _reserveSystem;
        private SystemHandle _benchmarkSystem;

        private EntityQuery _arenaQuery;
        private EntityQuery _contiguousQuery;
        private EntityQuery _chunkPagedQuery;
        private EntityQuery _sharedQuery;
        private EntityQuery _dynamicQuery;

        private NativeReference<long> _result;

        /// <summary>
        /// Long lived allocations made between buffer populations, standing in for everything else in a
        /// frame that touches the same allocator.
        ///
        /// Without them this harness creates 5000 DynamicBuffers back to back with nothing in between, so
        /// their heap blocks come back clustered and in creation order - which is also chunk iteration order.
        /// That hands DynamicBuffer the exact locality ArenaBuffer exists to provide, and no real workload
        /// would. The arena is unaffected either way: it owns its pages.
        /// </summary>
        private NativeList<IntPtr> _heapNoise;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            ArenaBufferRegistry.ResetAll();

            _reserveSystem = World.CreateSystem<ArenaBufferReserveSystem>();
            _benchmarkSystem = World.CreateSystem<ArenaBenchmarkSystem>();

            _arenaQuery = Manager.CreateEntityQuery(ComponentType.ReadWrite<ArenaTestElementRef>());
            _contiguousQuery = Manager.CreateEntityQuery(ComponentType.ReadWrite<ContiguousTestElementRef>());
            _dynamicQuery = Manager.CreateEntityQuery(ComponentType.ReadWrite<DynamicTestElement>());

            _result = new NativeReference<long>(Allocator.Persistent);
            _heapNoise = new NativeList<IntPtr>(EntityCount, Allocator.Persistent);
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

        private NativeArray<Entity> CreateArenaEntities(int elementsPerEntity)
        {
            var entities = new NativeArray<Entity>(EntityCount, Allocator.Persistent);

            for (var i = 0; i < EntityCount; i++)
            {
                var entity = Manager.CreateEntity();
                Manager.AddComponentData(entity, ArenaTestElementRef.Request(math.max(1, elementsPerEntity)));
                entities[i] = entity;
            }

            _reserveSystem.Update(World.Unmanaged);
            World.Unmanaged.ResetUpdateAllocator();

            var lookup = new ArenaTestElementLookup(Manager);
            var noise = new Random(0xC0FFEE);

            for (var i = 0; i < EntityCount; i++)
            {
                var buffer = lookup[entities[i]];
                for (var e = 0; e < elementsPerEntity; e++)
                {
                    buffer.Add(new ArenaTestElement { Value = e });
                }

                AllocateHeapNoise(ref noise);
            }

            Shuffle(entities);
            return entities;
        }

        /// <summary>
        /// Identical to <see cref="CreateArenaEntities"/> including the noise sequence, so the only thing
        /// separating the two arena benchmarks is which allocator resolves a handle.
        /// </summary>
        private NativeArray<Entity> CreateContiguousEntities(int elementsPerEntity)
        {
            var entities = new NativeArray<Entity>(EntityCount, Allocator.Persistent);

            for (var i = 0; i < EntityCount; i++)
            {
                var entity = Manager.CreateEntity();
                Manager.AddComponentData(entity, ContiguousTestElementRef.Request(math.max(1, elementsPerEntity)));
                entities[i] = entity;
            }

            _reserveSystem.Update(World.Unmanaged);
            World.Unmanaged.ResetUpdateAllocator();

            var lookup = new ContiguousTestElementLookup(Manager);
            var noise = new Random(0xC0FFEE);

            for (var i = 0; i < EntityCount; i++)
            {
                var buffer = lookup[entities[i]];
                for (var e = 0; e < elementsPerEntity; e++)
                {
                    buffer.Add(new ContiguousTestElement { Value = e });
                }

                AllocateHeapNoise(ref noise);
            }

            Shuffle(entities);
            return entities;
        }

        private NativeArray<Entity> CreateDynamicEntities(int elementsPerEntity)
        {
            var entities = new NativeArray<Entity>(EntityCount, Allocator.Persistent);
            var archetype = Manager.CreateArchetype(typeof(DynamicTestElement));
            var noise = new Random(0xC0FFEE);

            for (var i = 0; i < EntityCount; i++)
            {
                var entity = Manager.CreateEntity(archetype);
                var buffer = Manager.GetBuffer<DynamicTestElement>(entity);

                for (var e = 0; e < elementsPerEntity; e++)
                {
                    buffer.Add(new DynamicTestElement { Value = e });
                }

                entities[i] = entity;
                AllocateHeapNoise(ref noise);
            }

            Shuffle(entities);
            return entities;
        }

        /// <summary>
        /// Punches a hole between one entity's buffer block and the next. Applied identically to both sides
        /// so neither is favoured - the arena simply does not care.
        /// </summary>
        private void AllocateHeapNoise(ref Random random)
        {
            var bytes = random.NextInt(64, 4096);
            // UnsafeUtility rather than Memory.Unmanaged: the latter is internal to Unity.Collections and
            // NZCore.Tests has no asmref shim for it. Same underlying allocator either way.
            _heapNoise.Add((IntPtr)UnsafeUtility.Malloc(bytes, 16, Allocator.Persistent));
        }

        /// <summary>Randomises the visit order so the lookup benchmarks are genuinely random access.</summary>
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

        // ------------------------------------------------------------------------------------------------
        // Chunk iteration - the pair that isolates memory layout
        // ------------------------------------------------------------------------------------------------

        [Test]
        [Performance]
        public void ChunkSum_ArenaBuffer([Values(4, 64, 128, 256)] int elementsPerEntity)
        {
            var entities = CreateArenaEntities(elementsPerEntity);

            var job = new ArenaChunkSumJob
            {
                Handle = new ArenaTestElementTypeHandle(ref BenchmarkState, true),
                Result = _result
            };

            Measure
                .Method(() =>
                {
                    _result.Value = 0;
                    job.Handle.Update(ref BenchmarkState);
                    job.Run(_arenaQuery);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .SampleGroup(new SampleGroup($"ArenaBuffer.ChunkSum_{elementsPerEntity}", SampleUnit.Microsecond))
                .Run();

            AssertSum(elementsPerEntity);
            entities.Dispose();
        }

        [Test]
        [Performance]
        public void ChunkSum_ContiguousArenaBuffer([Values(4, 64, 128, 256)] int elementsPerEntity)
        {
            var entities = CreateContiguousEntities(elementsPerEntity);

            var job = new ContiguousChunkSumJob
            {
                Handle = new ContiguousTestElementTypeHandle(ref BenchmarkState, true),
                Result = _result
            };

            Measure
                .Method(() =>
                {
                    _result.Value = 0;
                    job.Handle.Update(ref BenchmarkState);
                    job.Run(_contiguousQuery);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .SampleGroup(new SampleGroup($"ContiguousArenaBuffer.ChunkSum_{elementsPerEntity}", SampleUnit.Microsecond))
                .Run();

            AssertSum(elementsPerEntity);
            entities.Dispose();
        }

        [Test]
        [Performance]
        public void ChunkSum_DynamicBuffer([Values(4, 64, 128, 256)] int elementsPerEntity)
        {
            var entities = CreateDynamicEntities(elementsPerEntity);

            var job = new DynamicChunkSumJob
            {
                Handle = BenchmarkState.GetBufferTypeHandle<DynamicTestElement>(true),
                Result = _result
            };

            Measure
                .Method(() =>
                {
                    _result.Value = 0;
                    job.Handle.Update(ref BenchmarkState);
                    job.Run(_dynamicQuery);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .SampleGroup(new SampleGroup($"DynamicBuffer.ChunkSum_{elementsPerEntity}", SampleUnit.Microsecond))
                .Run();

            AssertSum(elementsPerEntity);
            entities.Dispose();
        }

        // ------------------------------------------------------------------------------------------------
        // Random access by entity - both through a prebuilt lookup
        // ------------------------------------------------------------------------------------------------

        [Test]
        [Performance]
        public void LookupSum_ArenaBuffer([Values(4, 64, 128, 256)] int elementsPerEntity)
        {
            var entities = CreateArenaEntities(elementsPerEntity);

            var job = new ArenaLookupSumJob
            {
                Entities = entities,
                Lookup = new ArenaTestElementLookup(ref BenchmarkState, true),
                Result = _result
            };

            Measure
                .Method(() =>
                {
                    job.Run();
                }).SetUp(() =>
                {
                    job.Lookup.Update(ref BenchmarkState);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .SampleGroup(new SampleGroup($"ArenaBuffer.LookupSum_{elementsPerEntity}", SampleUnit.Microsecond))
                .Run();

            AssertSum(elementsPerEntity);
            entities.Dispose();
        }

        [Test]
        [Performance]
        public void LookupSum_ContiguousArenaBuffer([Values(4, 64, 128, 256)] int elementsPerEntity)
        {
            var entities = CreateContiguousEntities(elementsPerEntity);

            var job = new ContiguousLookupSumJob
            {
                Entities = entities,
                Lookup = new ContiguousTestElementLookup(ref BenchmarkState, true),
                Result = _result
            };

            Measure
                .Method(() =>
                {
                    job.Run();
                }).SetUp(() =>
                {
                    job.Lookup.Update(ref BenchmarkState);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .SampleGroup(new SampleGroup($"ContiguousArenaBuffer.LookupSum_{elementsPerEntity}", SampleUnit.Microsecond))
                .Run();

            AssertSum(elementsPerEntity);
            entities.Dispose();
        }

        [Test]
        [Performance]
        public void LookupSum_DynamicBuffer([Values(4, 64, 128, 256)] int elementsPerEntity)
        {
            var entities = CreateDynamicEntities(elementsPerEntity);

            var job = new DynamicLookupSumJob
            {
                Entities = entities,
                Lookup = BenchmarkState.GetBufferLookup<DynamicTestElement>(true),
                Result = _result
            };

            Measure
                .Method(() =>
                {
                    job.Run();
                }).SetUp(() =>
                {
                    job.Lookup.Update(ref BenchmarkState);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .SampleGroup(new SampleGroup($"DynamicBuffer.LookupSum_{elementsPerEntity}", SampleUnit.Microsecond))
                .Run();

            AssertSum(elementsPerEntity);
            entities.Dispose();
        }

        // ------------------------------------------------------------------------------------------------
        // Add churn - clear and refill every buffer, the path that exercises allocation
        // ------------------------------------------------------------------------------------------------

        [Test]
        [Performance]
        public void AddChurn_ArenaBuffer([Values(4, 64, 128, 256)] int elementsPerEntity)
        {
            var entities = CreateArenaEntities(0);

            var job = new ArenaChurnJob
            {
                Handle = new ArenaTestElementTypeHandle(ref BenchmarkState),
                ElementCount = elementsPerEntity
            };

            Measure
                .Method(() =>
                {
                    job.Handle.Update(ref BenchmarkState);
                    job.Run(_arenaQuery);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .SampleGroup(new SampleGroup($"ArenaBuffer.AddChurn_{elementsPerEntity}", SampleUnit.Microsecond))
                .Run();

            entities.Dispose();
        }

        [Test]
        [Performance]
        public void AddChurn_ContiguousArenaBuffer([Values(4, 64, 128, 256)] int elementsPerEntity)
        {
            var entities = CreateContiguousEntities(0);

            var job = new ContiguousChurnJob
            {
                Handle = new ContiguousTestElementTypeHandle(ref BenchmarkState),
                ElementCount = elementsPerEntity
            };

            Measure
                .Method(() =>
                {
                    job.Handle.Update(ref BenchmarkState);
                    job.Run(_contiguousQuery);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .SampleGroup(new SampleGroup($"ContiguousArenaBuffer.AddChurn_{elementsPerEntity}", SampleUnit.Microsecond))
                .Run();

            entities.Dispose();
        }

        [Test]
        [Performance]
        public void AddChurn_DynamicBuffer([Values(4, 64, 128, 256)] int elementsPerEntity)
        {
            var entities = CreateDynamicEntities(0);

            var job = new DynamicChurnJob
            {
                Handle = BenchmarkState.GetBufferTypeHandle<DynamicTestElement>(),
                ElementCount = elementsPerEntity
            };

            Measure
                .Method(() =>
                {
                    job.Handle.Update(ref BenchmarkState);
                    job.Run(_dynamicQuery);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .SampleGroup(new SampleGroup($"DynamicBuffer.AddChurn_{elementsPerEntity}", SampleUnit.Microsecond))
                .Run();

            entities.Dispose();
        }

        /// <summary>
        /// Guards against the measured loop being optimised away, and against a container silently reading
        /// the wrong thing - a benchmark that sums nothing is fast and meaningless.
        /// </summary>
        private void AssertSum(int elementsPerEntity)
        {
            var perEntity = (long)elementsPerEntity * (elementsPerEntity - 1) / 2;
            Assert.AreEqual(perEntity * EntityCount, _result.Value, "the benchmark did not read what it was supposed to");
        }

        [BurstCompile]
        private struct ArenaChunkSumJob : IJobChunk
        {
            [ReadOnly] public ArenaTestElementTypeHandle Handle;
            public NativeReference<long> Result;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var accessor = Handle.GetAccessor(chunk);
                long sum = 0;

                for (var i = 0; i < accessor.Length; i++)
                {
                    var buffer = accessor[i];

                    for (var e = 0; e < buffer.Length; e++)
                    {
                        sum += buffer[e].Value;
                    }
                }

                Result.Value += sum;
            }
        }

        [BurstCompile]
        private struct ContiguousChunkSumJob : IJobChunk
        {
            [ReadOnly] public ContiguousTestElementTypeHandle Handle;
            public NativeReference<long> Result;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var accessor = Handle.GetAccessor(chunk);
                long sum = 0;

                for (var i = 0; i < accessor.Length; i++)
                {
                    var buffer = accessor[i];

                    for (var e = 0; e < buffer.Length; e++)
                    {
                        sum += buffer[e].Value;
                    }
                }

                Result.Value += sum;
            }
        }

        [BurstCompile]
        private struct DynamicChunkSumJob : IJobChunk
        {
            [ReadOnly] public BufferTypeHandle<DynamicTestElement> Handle;
            public NativeReference<long> Result;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var accessor = chunk.GetBufferAccessor(ref Handle);
                long sum = 0;

                for (var i = 0; i < accessor.Length; i++)
                {
                    var buffer = accessor[i];

                    for (var e = 0; e < buffer.Length; e++)
                    {
                        sum += buffer[e].Value;
                    }
                }

                Result.Value += sum;
            }
        }

        [BurstCompile]
        private struct ArenaLookupSumJob : IJob
        {
            [ReadOnly] public NativeArray<Entity> Entities;
            [ReadOnly] public ArenaTestElementLookup Lookup;
            public NativeReference<long> Result;

            public void Execute()
            {
                long sum = 0;

                for (var i = 0; i < Entities.Length; i++)
                {
                    var buffer = Lookup[Entities[i]];

                    for (var e = 0; e < buffer.Length; e++)
                    {
                        sum += buffer[e].Value;
                    }
                }

                Result.Value = sum;
            }
        }

        [BurstCompile]
        private struct ContiguousLookupSumJob : IJob
        {
            [ReadOnly] public NativeArray<Entity> Entities;
            [ReadOnly] public ContiguousTestElementLookup Lookup;
            public NativeReference<long> Result;

            public void Execute()
            {
                long sum = 0;

                for (var i = 0; i < Entities.Length; i++)
                {
                    var buffer = Lookup[Entities[i]];

                    for (var e = 0; e < buffer.Length; e++)
                    {
                        sum += buffer[e].Value;
                    }
                }

                Result.Value = sum;
            }
        }

        [BurstCompile]
        private struct DynamicLookupSumJob : IJob
        {
            [ReadOnly] public NativeArray<Entity> Entities;
            [ReadOnly] public BufferLookup<DynamicTestElement> Lookup;
            public NativeReference<long> Result;

            public void Execute()
            {
                long sum = 0;

                for (var i = 0; i < Entities.Length; i++)
                {
                    var buffer = Lookup[Entities[i]];

                    for (var e = 0; e < buffer.Length; e++)
                    {
                        sum += buffer[e].Value;
                    }
                }

                Result.Value = sum;
            }
        }

        [BurstCompile]
        private struct ArenaChurnJob : IJobChunk
        {
            public ArenaTestElementTypeHandle Handle;
            public int ElementCount;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var accessor = Handle.GetAccessor(chunk);

                for (var i = 0; i < accessor.Length; i++)
                {
                    var buffer = accessor[i];
                    buffer.Clear();

                    for (var e = 0; e < ElementCount; e++)
                    {
                        buffer.Add(new ArenaTestElement { Value = e });
                    }
                }
            }
        }

        [BurstCompile]
        private struct ContiguousChurnJob : IJobChunk
        {
            public ContiguousTestElementTypeHandle Handle;
            public int ElementCount;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var accessor = Handle.GetAccessor(chunk);

                for (var i = 0; i < accessor.Length; i++)
                {
                    var buffer = accessor[i];
                    buffer.Clear();

                    for (var e = 0; e < ElementCount; e++)
                    {
                        buffer.Add(new ContiguousTestElement { Value = e });
                    }
                }
            }
        }

        [BurstCompile]
        private struct DynamicChurnJob : IJobChunk
        {
            public BufferTypeHandle<DynamicTestElement> Handle;
            public int ElementCount;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var accessor = chunk.GetBufferAccessor(ref Handle);

                for (var i = 0; i < accessor.Length; i++)
                {
                    var buffer = accessor[i];
                    buffer.Clear();

                    for (var e = 0; e < ElementCount; e++)
                    {
                        buffer.Add(new DynamicTestElement { Value = e });
                    }
                }
            }
        }
    }
}
