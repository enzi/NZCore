// <copyright project="NZCore.Tests" file="ChunkPagedArenaBufferTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace NZCore.Tests.NativeContainers
{
    // Element types have to sit at namespace scope: the generator emits the companion components next to
    // them and cannot reopen a non-partial test class.
    [ArenaBuffer(Mode = ArenaAllocatorMode.ChunkPaged, InitialCapacity = 0)]
    public struct ChunkPagedTestElement : IArenaBuffer
    {
        public int Value;
    }

    /// <summary>Only exists to put an entity in a second archetype, so it can be moved out of it later.</summary>
    public struct ChunkPagedTestTag : IComponentData
    {
    }

    /// <summary>
    /// This mode's speed comes from an assumption the accessor makes - that every record in a chunk lives on
    /// the chunk's page - and its safety comes from the buffer checking that assumption rather than trusting
    /// it. These tests are mostly about the cases where the assumption is false: entities that moved in, and
    /// buffers that outgrew the class their chunk page was carved at. Shared behaviour is already covered by
    /// <see cref="ArenaBufferTests"/>.
    /// </summary>
    public unsafe class ChunkPagedArenaBufferTests : EcsTestsFixture
    {
        private SystemHandle _reserveSystem;

        private static ChunkPagedArenaAllocator* Arena => ArenaBufferStorage<ChunkPagedTestElement>.GetChunkPagedArena();

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

        private Entity CreateArenaEntity(int capacity)
        {
            var entity = Manager.CreateEntity();
            Manager.AddComponentData(entity, ChunkPagedTestElementRef.Request(capacity));
            return entity;
        }

        private ChunkPagedArenaBuffer<ChunkPagedTestElement> GetBuffer(Entity entity)
        {
            var lookup = new ChunkPagedTestElementLookup(Manager);
            return lookup[entity];
        }

        /// <summary>Reads every buffer of every chunk the way a system would, through the chunk accessor.</summary>
        private long SumThroughChunkAccessor()
        {
            var query = Manager.CreateEntityQuery(ComponentType.ReadOnly<ChunkPagedTestElementRef>());
            var chunks = query.ToArchetypeChunkArray(Allocator.Temp);

            ref var state = ref World.Unmanaged.GetExistingSystemState<ArenaBufferReserveSystem>();
            var typeHandle = new ChunkPagedTestElementTypeHandle(ref state, true);

            long sum = 0;

            for (var c = 0; c < chunks.Length; c++)
            {
                var accessor = typeHandle.GetAccessor(chunks[c]);

                for (var i = 0; i < accessor.Length; i++)
                {
                    var buffer = accessor[i];

                    for (var e = 0; e < buffer.Length; e++)
                    {
                        sum += buffer[e].Value;
                    }
                }
            }

            chunks.Dispose();
            query.Dispose();
            return sum;
        }

        private int ReleaseAllAndDestroy(NativeArray<Entity> entities)
        {
            ref var state = ref World.Unmanaged.GetExistingSystemState<ArenaBufferReserveSystem>();

            var query = Manager.CreateEntityQuery(ComponentType.ReadWrite<ChunkPagedTestElementRef>());
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
        // The page per chunk arrangement
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The mode only pays off if a chunk's records really do end up on one page - that is what lets the
        /// accessor resolve it once. Nothing at runtime would notice if the reserve system fell back to
        /// per-entity allocation, so the arrangement has to be asserted directly.
        /// </summary>
        [Test]
        public void ReservingAChunk_PutsEveryRecordOnTheSamePage()
        {
            const int entityCount = 64;

            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            for (var i = 0; i < entityCount; i++)
            {
                entities[i] = CreateArenaEntity(8);
            }

            UpdateReserveSystem();

            var firstPage = Manager.GetComponentData<ChunkPagedTestElementRef>(entities[0]).Handle >> ChunkPagedArenaAllocator.PageShift;

            for (var i = 0; i < entityCount; i++)
            {
                var handle = Manager.GetComponentData<ChunkPagedTestElementRef>(entities[i]).Handle;
                Assert.AreEqual(firstPage, handle >> ChunkPagedArenaAllocator.PageShift, $"entity {i} landed on another page");
            }

            Assert.AreEqual(1, Arena->PageCount, "one reserved chunk should have taken exactly one page");

            ReleaseAllAndDestroy(entities);
            entities.Dispose();
        }

        /// <summary>
        /// One page means one size class, so a chunk holding a large buffer pads the small ones up to it.
        /// That is the memory cost of the mode and it should be visible rather than accidental.
        /// </summary>
        [Test]
        public void AChunksPage_UsesTheLargestCapacityAnyoneInItAskedFor()
        {
            var small = CreateArenaEntity(4);
            var large = CreateArenaEntity(256);
            var alsoSmall = CreateArenaEntity(8);

            UpdateReserveSystem();

            Assert.AreEqual(256, Manager.GetComponentData<ChunkPagedTestElementRef>(small).Capacity,
                "a small buffer sharing a chunk with a large one is padded to the chunk's class");
            Assert.AreEqual(256, Manager.GetComponentData<ChunkPagedTestElementRef>(large).Capacity);
            Assert.AreEqual(256, Manager.GetComponentData<ChunkPagedTestElementRef>(alsoSmall).Capacity);

            var entities = new NativeArray<Entity>(3, Allocator.Temp);
            entities[0] = small;
            entities[1] = large;
            entities[2] = alsoSmall;
            ReleaseAllAndDestroy(entities);
            entities.Dispose();
        }

        // ------------------------------------------------------------------------------------------------
        // Where the accessor's assumption breaks
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The accessor takes its page from the chunk's first record and every buffer compares against it. An
        /// entity that moved here from another chunk still points into its old page, so if the buffer trusted
        /// the accessor's page instead of checking it, this would read another entity's memory - silently.
        /// </summary>
        [Test]
        public void AnEntityThatMovedIntoTheChunk_StillReadsItsOwnBlock()
        {
            var settled = new NativeArray<Entity>(8, Allocator.Temp);
            for (var i = 0; i < settled.Length; i++)
            {
                settled[i] = CreateArenaEntity(16);
            }

            // A second archetype, reserved separately, so its page differs from the first group's.
            var mover = Manager.CreateEntity();
            Manager.AddComponentData(mover, ChunkPagedTestElementRef.Request(16));
            Manager.AddComponent<ChunkPagedTestTag>(mover);

            UpdateReserveSystem();

            for (var i = 0; i < settled.Length; i++)
            {
                var buffer = GetBuffer(settled[i]);
                buffer.Add(new ChunkPagedTestElement { Value = 1000 + i });
            }

            var moverBuffer = GetBuffer(mover);
            moverBuffer.Add(new ChunkPagedTestElement { Value = 777 });

            var moverPageBefore = Manager.GetComponentData<ChunkPagedTestElementRef>(mover).Handle >> ChunkPagedArenaAllocator.PageShift;
            var settledPage = Manager.GetComponentData<ChunkPagedTestElementRef>(settled[0]).Handle >> ChunkPagedArenaAllocator.PageShift;
            Assert.AreNotEqual(settledPage, moverPageBefore, "the two archetypes must start on different pages for this test to mean anything");

            // Moving it into the settled archetype rewrites its chunk but not its block.
            Manager.RemoveComponent<ChunkPagedTestTag>(mover);

            var moverAfter = Manager.GetComponentData<ChunkPagedTestElementRef>(mover);
            Assert.AreEqual(moverPageBefore, moverAfter.Handle >> ChunkPagedArenaAllocator.PageShift,
                "the record follows the entity, the block does not move with it");

            // Read everything the way a system does - through the chunk accessor, which resolved one page.
            long expected = 777;
            for (var i = 0; i < settled.Length; i++)
            {
                expected += 1000 + i;
            }

            Assert.AreEqual(expected, SumThroughChunkAccessor(), "a record from a foreign page must fall back rather than read the chunk's page");

            var all = new NativeArray<Entity>(settled.Length + 1, Allocator.Temp);
            for (var i = 0; i < settled.Length; i++)
            {
                all[i] = settled[i];
            }

            all[settled.Length] = mover;
            ReleaseAllAndDestroy(all);

            all.Dispose();
            settled.Dispose();
        }

        /// <summary>
        /// Growing past the chunk page's size class takes the block off an ordinary free list somewhere else,
        /// so the grown buffer stops matching its own chunk's page. Same hazard as a moved entity, reached a
        /// different way, and the one that happens without any structural change at all.
        /// </summary>
        [Test]
        public void ABufferThatOutgrowsItsChunkPage_StillReadsBackThroughTheAccessor()
        {
            const int entityCount = 16;

            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            for (var i = 0; i < entityCount; i++)
            {
                entities[i] = CreateArenaEntity(4);
            }

            UpdateReserveSystem();

            long expected = 0;

            for (var i = 0; i < entityCount; i++)
            {
                var buffer = GetBuffer(entities[i]);

                // Entity 3 grows far past the chunk's class and must leave the page; the rest stay put.
                var count = i == 3 ? 400 : 4;

                for (var e = 0; e < count; e++)
                {
                    buffer.Add(new ChunkPagedTestElement { Value = e });
                    expected += e;
                }
            }

            var grownPage = Manager.GetComponentData<ChunkPagedTestElementRef>(entities[3]).Handle >> ChunkPagedArenaAllocator.PageShift;
            var stayedPage = Manager.GetComponentData<ChunkPagedTestElementRef>(entities[0]).Handle >> ChunkPagedArenaAllocator.PageShift;
            Assert.AreNotEqual(stayedPage, grownPage, "a buffer past the chunk's size class has to have left the chunk page");

            Assert.AreEqual(expected, SumThroughChunkAccessor(), "the grown buffer must still read its own block");

            ReleaseAllAndDestroy(entities);
            entities.Dispose();
        }

        // ------------------------------------------------------------------------------------------------
        // Parity with DynamicBuffer
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The API is a DynamicBuffer replacement, so the only definition of correct is that it behaves like
        /// one. The random sequence repeatedly crosses size classes, which is exactly what moves a buffer off
        /// its chunk page and back, so the page check is exercised throughout rather than incidentally.
        /// </summary>
        [Test]
        public void ChunkPagedArenaBuffer_MatchesDynamicBuffer_ThroughARandomOperationSequence()
        {
            var arenaEntity = CreateArenaEntity(1);
            var dynamicEntity = Manager.CreateEntity();
            Manager.AddBuffer<DynamicTestElement>(dynamicEntity);

            UpdateReserveSystem();

            var random = new Random(20260726);

            for (var step = 0; step < 500; step++)
            {
                var arena = GetBuffer(arenaEntity);
                var dynamicBuffer = Manager.GetBuffer<DynamicTestElement>(dynamicEntity);

                switch (random.NextInt(0, 6))
                {
                    case 0:
                    {
                        var value = random.NextInt();
                        arena.Add(new ChunkPagedTestElement { Value = value });
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
                        arena.Insert(index, new ChunkPagedTestElement { Value = value });
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

            Manager.DestroyEntity(dynamicEntity);

            var single = new NativeArray<Entity>(1, Allocator.Temp);
            single[0] = arenaEntity;
            ReleaseAllAndDestroy(single);
            single.Dispose();
        }

        // ------------------------------------------------------------------------------------------------
        // Release
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Release runs untyped over every registered element type, so it has to free through the allocator
        /// the registration's mode names. Blocks from a chunk page go back onto ordinary free lists, which is
        /// what lets a destroyed chunk's page be reused piecemeal.
        /// </summary>
        [Test]
        public void ReleasingAChunk_ReturnsItsBlocksToTheArena()
        {
            const int entityCount = 32;

            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            for (var i = 0; i < entityCount; i++)
            {
                entities[i] = CreateArenaEntity(32);
            }

            UpdateReserveSystem();
            Assert.AreEqual(entityCount, Arena->LiveBlocks);

            var freed = ReleaseAllAndDestroy(entities);

            Assert.AreEqual(entityCount, freed, "every block should have been handed back");
            Assert.AreEqual(0, Arena->LiveBlocks, "the arena still thinks blocks are live");

            entities.Dispose();
        }

        [Test]
        public void DestroyingAWorld_FreesOnlyThatWorldsBlocks()
        {
            var keptEntity = CreateArenaEntity(16);
            UpdateReserveSystem();

            var otherWorld = new World("Chunk Paged Arena Teardown Test World");
            var otherSystem = otherWorld.CreateSystem<ArenaBufferReserveSystem>();

            var otherEntity = otherWorld.EntityManager.CreateEntity();
            otherWorld.EntityManager.AddComponentData(otherEntity, ChunkPagedTestElementRef.Request(32));
            otherSystem.Update(otherWorld.Unmanaged);

            Assert.AreEqual(2, Arena->LiveBlocks);

            otherWorld.Dispose();

            Assert.AreEqual(1, Arena->LiveBlocks, "teardown must free that World's block and leave this World's alone");
            Assert.IsTrue(Manager.GetComponentData<ChunkPagedTestElementRef>(keptEntity).IsReserved);
        }
    }
}
