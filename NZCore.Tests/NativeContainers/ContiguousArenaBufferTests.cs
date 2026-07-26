// <copyright project="NZCore.Tests" file="ContiguousArenaBufferTests.cs">
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
    [ArenaBuffer(Mode = ArenaAllocatorMode.Contiguous, InitialCapacity = 0)]
    public struct ContiguousTestElement : IArenaBuffer
    {
        public int Value;
    }

    /// <summary>
    /// The contiguous allocator differs from the paged one in exactly two places - a handle is a byte offset
    /// rather than a page index plus offset, and the backing block moves when it grows. Everything else is
    /// shared code already covered by <see cref="ArenaBufferTests"/>, so these tests concentrate on those two
    /// and on proving the generator wired the right allocator up.
    /// </summary>
    public unsafe class ContiguousArenaBufferTests : EcsTestsFixture
    {
        private SystemHandle _reserveSystem;

        private static ContiguousArenaAllocator* Arena => ArenaBufferStorage<ContiguousTestElement>.GetContiguousArena();

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
            Manager.AddComponentData(entity, ContiguousTestElementRef.Request(capacity));
            return entity;
        }

        private ContiguousArenaBuffer<ContiguousTestElement> GetBuffer(Entity entity)
        {
            var lookup = new ContiguousTestElementLookup(Manager);
            return lookup[entity];
        }

        private int ReleaseAllAndDestroy(NativeArray<Entity> entities)
        {
            ref var state = ref World.Unmanaged.GetExistingSystemState<ArenaBufferReserveSystem>();

            var query = Manager.CreateEntityQuery(ComponentType.ReadWrite<ContiguousTestElementRef>());
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
        // Wiring
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The mode is chosen by an attribute the generator reads, so nothing at runtime would notice if it
        /// silently fell back to paged. A block handed out by the contiguous allocator is a plain byte offset
        /// into the block, which the paged encoding could not produce for anything past the first page.
        /// </summary>
        [Test]
        public void TheGeneratedTypesAreWiredToTheContiguousAllocator()
        {
            var entity = CreateArenaEntity(4);
            UpdateReserveSystem();

            Assert.IsTrue(Arena->IsCreated, "the registration created a contiguous arena for this element type");

            var handle = Manager.GetComponentData<ContiguousTestElementRef>(entity).Handle;
            var stride = Arena->BlockStride(ContiguousArenaAllocator.SizeClassOf(4));

            Assert.AreEqual(0, handle % stride, "a contiguous handle is a byte offset, so it lands on a block boundary");
            Assert.Less((long)handle, Arena->CarvedBytes, "and it points inside the carved part of the block");
        }

        /// <summary>
        /// Baking cannot touch arena memory, so a request only becomes a real block once the reserve system
        /// runs - the same contract as the paged buffer, driven through the same untyped reserve system, which
        /// has to pick the right allocator from the registration's mode to honour it.
        /// </summary>
        [Test]
        public void Request_BecomesAReservedBlockOnlyAfterTheReserveSystemRuns()
        {
            var entity = CreateArenaEntity(8);

            Assert.IsFalse(Manager.GetComponentData<ContiguousTestElementRef>(entity).IsReserved);
            Assert.AreEqual(0, Arena->LiveBlocks, "no arena memory may be handed out before the system runs");

            UpdateReserveSystem();

            var refData = Manager.GetComponentData<ContiguousTestElementRef>(entity);
            Assert.IsTrue(refData.IsReserved);
            Assert.AreEqual(0, refData.Length, "a reserved buffer starts empty, capacity is not content");
            Assert.AreEqual(8, refData.Capacity);
            Assert.AreEqual(1, Arena->LiveBlocks);
        }

        // ------------------------------------------------------------------------------------------------
        // The moving arena - what this allocator trades away
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// This is the contiguous allocator's whole risk. Growing past the tail reallocates the backing block
        /// and copies it, which moves every live block at once. Handles are offsets so they survive, but if
        /// anything cached an address or if the copy were partial, buffers written before the move would read
        /// back garbage. Nothing else in the design catches that.
        /// </summary>
        [Test]
        public void WhenTheArenaOutgrowsItsBlockAndMoves_EveryBufferWrittenBeforeTheMoveStillReadsBack()
        {
            const int entityCount = 200;
            const int elementsEach = 512;

            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);

            // Capacity 1, deliberately: asking for elementsEach up front would have the reserve system size
            // the whole arena before the fill, and then nothing below would ever trigger a reallocation.
            for (var i = 0; i < entityCount; i++)
            {
                entities[i] = CreateArenaEntity(1);
            }

            UpdateReserveSystem();

            var bytesBefore = Arena->AllocatedBytes;

            // Fill as we go, so most of these entities are written before the reallocations that later
            // entities trigger. A buffer filled at i == 0 has to survive every move that follows it.
            for (var i = 0; i < entityCount; i++)
            {
                var buffer = GetBuffer(entities[i]);

                for (var e = 0; e < elementsEach; e++)
                {
                    buffer.Add(new ContiguousTestElement { Value = i * elementsEach + e });
                }
            }

            Assert.Greater(Arena->AllocatedBytes, bytesBefore,
                "the test is only meaningful if the arena actually reallocated - raise entityCount if this fires");

            for (var i = 0; i < entityCount; i++)
            {
                var buffer = GetBuffer(entities[i]);
                Assert.AreEqual(elementsEach, buffer.Length, $"entity {i} lost elements across an arena move");

                for (var e = 0; e < elementsEach; e++)
                {
                    Assert.AreEqual(i * elementsEach + e, buffer[e].Value, $"entity {i} element {e} did not survive the move");
                }
            }

            ReleaseAllAndDestroy(entities);
            entities.Dispose();
        }

        /// <summary>
        /// The free lists live inside the free blocks as offsets, so a move relocates the chains along with
        /// everything else. If they held addresses instead, the first allocation after a move would hand back
        /// a pointer into the freed block - silently, since the counters would still look right.
        /// </summary>
        [Test]
        public void FreeListsSurviveAnArenaMove_AndStillHandOutUsableBlocks()
        {
            const int entityCount = 120;
            const int elementsEach = 512;

            // Capacity 1 for the same reason as above: the growth has to happen after the snapshot below,
            // not during reservation.
            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            for (var i = 0; i < entityCount; i++)
            {
                entities[i] = CreateArenaEntity(1);
            }

            UpdateReserveSystem();

            // Free a block, then force growth, then take a block again: the recycled one comes off a chain
            // that was written before the move and read after it.
            var recycled = GetBuffer(entities[0]);
            recycled.Capacity = elementsEach * 2; // reallocates, pushing the old block onto its free list

            var bytesBefore = Arena->AllocatedBytes;

            for (var i = 1; i < entityCount; i++)
            {
                var buffer = GetBuffer(entities[i]);
                buffer.Resize(elementsEach, NativeArrayOptions.ClearMemory);
            }

            Assert.Greater(Arena->AllocatedBytes, bytesBefore, "expected the arena to have grown by now");

            var reuser = GetBuffer(entities[1]);
            reuser.Clear();

            for (var e = 0; e < elementsEach; e++)
            {
                reuser.Add(new ContiguousTestElement { Value = e * 7 });
            }

            for (var e = 0; e < elementsEach; e++)
            {
                Assert.AreEqual(e * 7, reuser[e].Value, "a block taken from a chain that survived a move is not usable");
            }

            ReleaseAllAndDestroy(entities);
            entities.Dispose();
        }

        // ------------------------------------------------------------------------------------------------
        // Parity with DynamicBuffer
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The API is a DynamicBuffer replacement, so the only definition of correct is that it behaves like
        /// one. A scripted random sequence covers the interactions between grow, shrink and remove that
        /// individual tests do not - and here it also drives repeated arena growth, so the parity is being
        /// checked across moves rather than in a static arena.
        /// </summary>
        [Test]
        public void ContiguousArenaBuffer_MatchesDynamicBuffer_ThroughARandomOperationSequence()
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
                        arena.Add(new ContiguousTestElement { Value = value });
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
                        arena.Insert(index, new ContiguousTestElement { Value = value });
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
        /// Release runs untyped over every registered element type at once, so it has to read the mode from
        /// the registration to free through the right allocator. Freeing a contiguous handle as if it were a
        /// paged one would push a bogus offset onto a chain and corrupt the arena rather than fail loudly.
        /// </summary>
        [Test]
        public void ReleasingAChunk_ReturnsContiguousBlocksToTheArena()
        {
            const int entityCount = 16;

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

        /// <summary>
        /// Arenas are process global, so teardown has to hand back exactly the blocks of the World going away.
        /// It walks the records untyped, which means it too has to free through the allocator the mode names.
        /// </summary>
        [Test]
        public void DestroyingAWorld_FreesOnlyThatWorldsContiguousBlocks()
        {
            var keptEntity = CreateArenaEntity(16);
            UpdateReserveSystem();

            var otherWorld = new World("Contiguous Arena Teardown Test World");
            var otherSystem = otherWorld.CreateSystem<ArenaBufferReserveSystem>();

            var otherEntity = otherWorld.EntityManager.CreateEntity();
            otherWorld.EntityManager.AddComponentData(otherEntity, ContiguousTestElementRef.Request(32));
            otherSystem.Update(otherWorld.Unmanaged);

            Assert.AreEqual(2, Arena->LiveBlocks);

            otherWorld.Dispose();

            Assert.AreEqual(1, Arena->LiveBlocks, "teardown must free that World's block and leave this World's alone");
            Assert.IsTrue(Manager.GetComponentData<ContiguousTestElementRef>(keptEntity).IsReserved);
        }
    }
}
