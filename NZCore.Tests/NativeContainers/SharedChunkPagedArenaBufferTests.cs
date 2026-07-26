// <copyright project="NZCore.Tests" file="SharedChunkPagedArenaBufferTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace NZCore.Tests.NativeContainers
{
    // Element types have to sit at namespace scope: the generator emits the companion components next to
    // them and cannot reopen a non-partial test class. Two types with different element sizes, because the
    // shared arena is byte oriented and a same-sized pair would hide any element size mistake.
    [ArenaBuffer(Mode = ArenaAllocatorMode.SharedChunkPaged, InitialCapacity = 0)]
    public struct SharedTestElementA : IArenaBuffer
    {
        public int Value;
    }

    [ArenaBuffer(Mode = ArenaAllocatorMode.SharedChunkPaged, InitialCapacity = 0)]
    public struct SharedTestElementB : IArenaBuffer
    {
        public long Value;
        public int Extra;
    }

    /// <summary>
    /// What this mode adds over <see cref="ChunkPagedArenaBufferTests"/> is that a chunk's buffers of
    /// <b>different</b> element types share one page, out of one byte oriented arena. These tests are about
    /// that: the co-location itself, and the element size conversions the shared arena cannot do for itself.
    /// </summary>
    public unsafe class SharedChunkPagedArenaBufferTests : EcsTestsFixture
    {
        private SystemHandle _reserveSystem;

        private static SharedArenaAllocator* Arena => ArenaBufferStorage<SharedTestElementA>.GetSharedArena();

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            // Arenas are process global rather than per World, so every test has to start from an empty one.
            ArenaBufferRegistry.ResetAll();

            _reserveSystem = World.CreateSystem<ArenaBufferReserveSystem>();
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            ArenaBufferRegistry.ResetAll();
        }

        private void UpdateReserveSystem()
        {
            _reserveSystem.Update(World.Unmanaged);
            World.Unmanaged.ResetUpdateAllocator();
        }

        /// <summary>An entity carrying both shared buffer types, which is the arrangement the mode targets.</summary>
        private Entity CreateEntityWithBoth(int capacityA, int capacityB)
        {
            var entity = Manager.CreateEntity();
            Manager.AddComponentData(entity, SharedTestElementARef.Request(capacityA));
            Manager.AddComponentData(entity, SharedTestElementBRef.Request(capacityB));
            return entity;
        }

        private SharedChunkPagedArenaBuffer<SharedTestElementA> GetA(Entity entity)
        {
            var lookup = new SharedTestElementALookup(Manager);
            return lookup[entity];
        }

        private SharedChunkPagedArenaBuffer<SharedTestElementB> GetB(Entity entity)
        {
            var lookup = new SharedTestElementBLookup(Manager);
            return lookup[entity];
        }

        private static int PageOf(int handle)
        {
            return handle >> SharedArenaAllocator.PageShift;
        }

        private int ReleaseAllAndDestroy(NativeArray<Entity> entities)
        {
            ref var state = ref World.Unmanaged.GetExistingSystemState<ArenaBufferReserveSystem>();

            var query = Manager.CreateEntityQuery(ComponentType.ReadWrite<SharedTestElementARef>());
            var chunks = query.ToArchetypeChunkArray(Allocator.Temp);

            var handles = ArenaBufferReleaseHandles.Create(ref state);
            var freed = 0;

            for (var i = 0; i < chunks.Length; i++)
            {
                freed += handles.ReleaseChunk(chunks[i]);
            }

            handles.Dispose();
            chunks.Dispose();
            query.Dispose();

            Manager.DestroyEntity(entities);
            return freed;
        }

        // ------------------------------------------------------------------------------------------------
        // The point of the mode
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The whole reason this mode exists. Under every other mode each element type has its own arena, so
        /// two buffers on one entity are in unrelated memory. Here a chunk takes one page and lays both types
        /// into it, so an entity's buffers of different types are within a page of each other.
        /// </summary>
        [Test]
        public void BuffersOfDifferentTypesInOneChunk_ShareAPage()
        {
            const int entityCount = 32;

            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            for (var i = 0; i < entityCount; i++)
            {
                entities[i] = CreateEntityWithBoth(8, 8);
            }

            UpdateReserveSystem();

            var page = PageOf(Manager.GetComponentData<SharedTestElementARef>(entities[0]).Handle);

            for (var i = 0; i < entityCount; i++)
            {
                Assert.AreEqual(page, PageOf(Manager.GetComponentData<SharedTestElementARef>(entities[i]).Handle), $"entity {i} type A left the chunk page");
                Assert.AreEqual(page, PageOf(Manager.GetComponentData<SharedTestElementBRef>(entities[i]).Handle), $"entity {i} type B left the chunk page");
            }

            Assert.AreEqual(1, Arena->PageCount, "one chunk of two types should have taken exactly one page");
            Assert.AreEqual(entityCount * 2, Arena->LiveBlocks, "both types' blocks are accounted for");

            ReleaseAllAndDestroy(entities);
            entities.Dispose();
        }

        /// <summary>
        /// Each type gets a contiguous run inside the shared page, so chunk iteration of one type is still
        /// sequential. Interleaving the types instead would put every read a stride apart and undo the layout
        /// the arena exists for.
        /// </summary>
        [Test]
        public void EachTypeGetsAContiguousRunWithinTheSharedPage()
        {
            const int entityCount = 16;

            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            for (var i = 0; i < entityCount; i++)
            {
                entities[i] = CreateEntityWithBoth(4, 4);
            }

            UpdateReserveSystem();

            var strideA = SharedArenaAllocator.BlockBytes(4, sizeof(int));
            var offsets = new NativeArray<int>(entityCount, Allocator.Temp);

            for (var i = 0; i < entityCount; i++)
            {
                offsets[i] = Manager.GetComponentData<SharedTestElementARef>(entities[i]).Handle & (SharedArenaAllocator.MaxPageBytes - 1);
            }

            var min = offsets[0];
            var max = offsets[0];

            for (var i = 1; i < entityCount; i++)
            {
                min = math.min(min, offsets[i]);
                max = math.max(max, offsets[i]);
            }

            Assert.AreEqual((entityCount - 1) * strideA, max - min,
                "type A's blocks should span exactly its own run, with nothing else interleaved into it");

            offsets.Dispose();
            ReleaseAllAndDestroy(entities);
            entities.Dispose();
        }

        // ------------------------------------------------------------------------------------------------
        // Byte oriented bookkeeping
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The shared arena stores no element size, so freeing has to rebuild a block's byte size from the
        /// record's capacity and the type's registered element size. Get that wrong for either type and the
        /// block lands on the wrong free list, which corrupts the arena silently rather than failing. The two
        /// test types have different element sizes precisely so this can go wrong.
        /// </summary>
        [Test]
        public void ReleasingBothTypes_ReturnsEveryBlockToTheArena()
        {
            const int entityCount = 24;

            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            for (var i = 0; i < entityCount; i++)
            {
                entities[i] = CreateEntityWithBoth(16, 16);
            }

            UpdateReserveSystem();
            Assert.AreEqual(entityCount * 2, Arena->LiveBlocks);

            var freed = ReleaseAllAndDestroy(entities);

            Assert.AreEqual(entityCount * 2, freed, "both types' blocks should have been handed back");
            Assert.AreEqual(0, Arena->LiveBlocks, "the arena still thinks blocks are live");
            Assert.AreEqual(0, Arena->UsedBytes, "used bytes must come back to zero, not just the block count");

            entities.Dispose();
        }

        /// <summary>
        /// A block's byte size is derived from capacity times element size, so the two types must not collide
        /// even when they ask for the same capacity - B's elements are three times A's.
        /// </summary>
        [Test]
        public void TypesWithDifferentElementSizes_GetDifferentlySizedBlocks()
        {
            var entity = CreateEntityWithBoth(8, 8);
            UpdateReserveSystem();

            var a = GetA(entity);
            var b = GetB(entity);

            for (var i = 0; i < 8; i++)
            {
                a.Add(new SharedTestElementA { Value = i });
                b.Add(new SharedTestElementB { Value = i * 1000L, Extra = i });
            }

            for (var i = 0; i < 8; i++)
            {
                Assert.AreEqual(i, a[i].Value, $"type A element {i} was overwritten");
                Assert.AreEqual(i * 1000L, b[i].Value, $"type B element {i} was overwritten");
                Assert.AreEqual(i, b[i].Extra, $"type B element {i} extra field was overwritten");
            }

            var single = new NativeArray<Entity>(1, Allocator.Temp);
            single[0] = entity;
            ReleaseAllAndDestroy(single);
            single.Dispose();
        }

        // ------------------------------------------------------------------------------------------------
        // Where the page assumption breaks
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Growing past the class its chunk page was laid out at takes an ordinary free list block elsewhere,
        /// so the buffer stops matching its own chunk's page. Same hazard as the per type chunk paged mode,
        /// and the reason the buffer compares the page index rather than trusting it.
        /// </summary>
        [Test]
        public void ABufferThatOutgrowsTheSharedPage_StillReadsBack()
        {
            const int entityCount = 8;

            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            for (var i = 0; i < entityCount; i++)
            {
                entities[i] = CreateEntityWithBoth(4, 4);
            }

            UpdateReserveSystem();

            var pageBefore = PageOf(Manager.GetComponentData<SharedTestElementARef>(entities[2]).Handle);

            var grown = GetA(entities[2]);
            for (var e = 0; e < 500; e++)
            {
                grown.Add(new SharedTestElementA { Value = e });
            }

            Assert.AreNotEqual(pageBefore, PageOf(Manager.GetComponentData<SharedTestElementARef>(entities[2]).Handle),
                "a buffer past the page's class has to have left it");

            var reread = GetA(entities[2]);
            Assert.AreEqual(500, reread.Length);

            for (var e = 0; e < 500; e++)
            {
                Assert.AreEqual(e, reread[e].Value, $"element {e} did not survive leaving the shared page");
            }

            // The neighbours it was laid out next to must be untouched by the move.
            for (var i = 0; i < entityCount; i++)
            {
                if (i == 2)
                {
                    continue;
                }

                Assert.AreEqual(0, GetA(entities[i]).Length, $"entity {i} was disturbed by its neighbour growing");
            }

            ReleaseAllAndDestroy(entities);
            entities.Dispose();
        }

        // ------------------------------------------------------------------------------------------------
        // Parity with DynamicBuffer
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The API is a DynamicBuffer replacement, so the only definition of correct is that it behaves like
        /// one. Driven on type A while type B sits beside it in the same page, so a stride or offset mistake
        /// in the shared layout shows up as divergence rather than passing unnoticed.
        /// </summary>
        [Test]
        public void SharedChunkPagedArenaBuffer_MatchesDynamicBuffer_ThroughARandomOperationSequence()
        {
            var arenaEntity = CreateEntityWithBoth(1, 8);
            var dynamicEntity = Manager.CreateEntity();
            Manager.AddBuffer<DynamicTestElement>(dynamicEntity);

            UpdateReserveSystem();

            // Fill the neighbour, so anything overrunning type A's run corrupts recognisable data.
            var neighbour = GetB(arenaEntity);
            for (var i = 0; i < 8; i++)
            {
                neighbour.Add(new SharedTestElementB { Value = -1, Extra = -1 });
            }

            var random = new Random(20260726);

            for (var step = 0; step < 500; step++)
            {
                var arena = GetA(arenaEntity);
                var dynamicBuffer = Manager.GetBuffer<DynamicTestElement>(dynamicEntity);

                switch (random.NextInt(0, 6))
                {
                    case 0:
                    {
                        var value = random.NextInt();
                        arena.Add(new SharedTestElementA { Value = value });
                        dynamicBuffer.Add(new DynamicTestElement { Value = value });
                        break;
                    }
                    case 1 when dynamicBuffer.Length > 0:
                    {
                        var index = random.NextInt(0, dynamicBuffer.Length);
                        arena.RemoveAt(index);
                        dynamicBuffer.RemoveAt(index);
                        break;
                    }
                    case 2 when dynamicBuffer.Length > 0:
                    {
                        var index = random.NextInt(0, dynamicBuffer.Length);
                        arena.RemoveAtSwapBack(index);
                        dynamicBuffer.RemoveAtSwapBack(index);
                        break;
                    }
                    case 3:
                    {
                        var index = random.NextInt(0, dynamicBuffer.Length + 1);
                        var value = random.NextInt();
                        arena.Insert(index, new SharedTestElementA { Value = value });
                        dynamicBuffer.Insert(index, new DynamicTestElement { Value = value });
                        break;
                    }
                    case 4:
                    {
                        var length = random.NextInt(0, 400);
                        arena.Resize(length, NativeArrayOptions.ClearMemory);
                        dynamicBuffer.Resize(length, NativeArrayOptions.ClearMemory);
                        break;
                    }
                    default:
                    {
                        arena.Clear();
                        dynamicBuffer.Clear();
                        break;
                    }
                }

                Assert.AreEqual(dynamicBuffer.Length, arena.Length, $"lengths diverged at step {step}");

                for (var i = 0; i < dynamicBuffer.Length; i++)
                {
                    Assert.AreEqual(dynamicBuffer[i].Value, arena[i].Value, $"element {i} diverged at step {step}");
                }
            }

            var check = GetB(arenaEntity);
            Assert.AreEqual(8, check.Length, "the neighbouring type's buffer was resized by type A's churn");

            for (var i = 0; i < 8; i++)
            {
                Assert.AreEqual(-1, check[i].Value, $"the neighbouring type's element {i} was overwritten");
            }

            Manager.DestroyEntity(dynamicEntity);

            var single = new NativeArray<Entity>(1, Allocator.Temp);
            single[0] = arenaEntity;
            ReleaseAllAndDestroy(single);
            single.Dispose();
        }

        [Test]
        public void DestroyingAWorld_FreesOnlyThatWorldsBlocks()
        {
            var kept = CreateEntityWithBoth(16, 16);
            UpdateReserveSystem();

            var otherWorld = new World("Shared Arena Teardown Test World");
            var otherSystem = otherWorld.CreateSystem<ArenaBufferReserveSystem>();

            var otherEntity = otherWorld.EntityManager.CreateEntity();
            otherWorld.EntityManager.AddComponentData(otherEntity, SharedTestElementARef.Request(32));
            otherSystem.Update(otherWorld.Unmanaged);

            Assert.AreEqual(3, Arena->LiveBlocks, "two blocks here plus one there");

            otherWorld.Dispose();

            Assert.AreEqual(2, Arena->LiveBlocks, "teardown must free that World's block and leave this World's alone");
            Assert.IsTrue(Manager.GetComponentData<SharedTestElementARef>(kept).IsReserved);
            Assert.IsTrue(Manager.GetComponentData<SharedTestElementBRef>(kept).IsReserved);
        }
    }
}
