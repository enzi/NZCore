// <copyright project="NZCore.Tests" file="ArenaBufferTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace NZCore.Tests.NativeContainers
{
    // Element types have to sit at namespace scope: the generator emits the companion components next to
    // them and cannot reopen a non-partial test class.
    public struct ArenaTestElement : IArenaBuffer
    {
        public int Value;
    }

    [InternalBufferCapacity(8)]
    public struct DynamicTestElement : IBufferElementData
    {
        public int Value;
    }

    public unsafe class ArenaBufferTests : EcsTestsFixture
    {
        private SystemHandle _reserveSystem;

        private static ArenaAllocator* Arena => ArenaBufferStorage<ArenaTestElement>.GetArena();

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
            Manager.AddComponentData(entity, ArenaTestElementRef.Request(capacity));
            return entity;
        }

        /// <summary>
        /// Stands in for the project's DestroyEntity pipeline: free the blocks, then destroy. Releases every
        /// chunk holding the ref type, so it is only valid when all of them are going away.
        /// </summary>
        private int ReleaseAllAndDestroy(NativeArray<Entity> entities)
        {
            ref var state = ref World.Unmanaged.GetExistingSystemState<ArenaBufferReserveSystem>();

            var query = Manager.CreateEntityQuery(ComponentType.ReadWrite<ArenaTestElementRef>());
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

        private int ReleaseAllAndDestroy(Entity entity)
        {
            var single = new NativeArray<Entity>(1, Allocator.Temp);
            single[0] = entity;
            var freed = ReleaseAllAndDestroy(single);
            single.Dispose();
            return freed;
        }

        private ArenaBuffer<ArenaTestElement> GetBuffer(Entity entity)
        {
            var lookup = new ArenaTestElementLookup(Manager);
            return lookup[entity];
        }

        // ------------------------------------------------------------------------------------------------
        // Reservation lifecycle
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Baking cannot touch arena memory, so a request only becomes a real block once the reserve system
        /// runs. Code that reads a buffer before that would silently see a foreign block if the reservation
        /// were skipped, so the boundary has to be observable.
        /// </summary>
        [Test]
        public void Request_BecomesAReservedBlockOnlyAfterTheReserveSystemRuns()
        {
            var entity = CreateArenaEntity(8);

            var requested = Manager.GetComponentData<ArenaTestElementRef>(entity);
            Assert.IsFalse(requested.IsReserved, "the record starts unreserved, it only carries the wanted capacity");
            Assert.AreEqual(8, requested.Capacity, "an unreserved record carries the capacity it is asking for");
            Assert.AreEqual(0, Arena->LiveBlocks, "no arena memory may be handed out before the system runs");

            UpdateReserveSystem();

            var refData = Manager.GetComponentData<ArenaTestElementRef>(entity);
            Assert.IsTrue(refData.IsReserved);
            Assert.AreEqual(0, refData.Length, "a reserved buffer starts empty, capacity is not content");
            Assert.AreEqual(8, refData.Capacity);
            Assert.AreEqual(1, Arena->LiveBlocks);
        }

        /// <summary>
        /// The reserve query is order version filtered so it does not walk every arena entity each frame.
        /// That optimisation is only safe if entities created after the system last ran are still picked up,
        /// which is exactly what this checks.
        /// </summary>
        [Test]
        public void EntitiesCreatedAfterAnUpdate_AreStillReservedOnTheNextOne()
        {
            var first = CreateArenaEntity(4);
            UpdateReserveSystem();
            Assert.IsTrue(Manager.GetComponentData<ArenaTestElementRef>(first).IsReserved);

            var second = CreateArenaEntity(4);
            UpdateReserveSystem();

            Assert.IsTrue(Manager.GetComponentData<ArenaTestElementRef>(second).IsReserved,
                "the order version filter must not hide entities added after the previous update");
        }

        /// <summary>
        /// Capacities are power of two size classes. Callers need to know their requested 5 became an 8,
        /// because that is the number Capacity reports back and the number the free lists key on.
        /// </summary>
        [Test]
        public void RequestedCapacity_IsRoundedUpToASizeClass()
        {
            var entity = CreateArenaEntity(5);
            UpdateReserveSystem();

            Assert.AreEqual(8, GetBuffer(entity).Capacity);
        }

        // ------------------------------------------------------------------------------------------------
        // Parity with DynamicBuffer
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The point of ArenaBuffer is to be a drop in replacement, so the same sequence of operations has
        /// to leave it holding exactly what a DynamicBuffer holds. Comparing after every single step means a
        /// divergence is reported at the operation that caused it rather than 400 steps later.
        /// </summary>
        [Test]
        public void ArenaBuffer_MatchesDynamicBuffer_ThroughARandomOperationSequence()
        {
            var arenaEntity = CreateArenaEntity(4);
            var dynamicEntity = Manager.CreateEntity(typeof(DynamicTestElement));
            UpdateReserveSystem();

            var random = new Unity.Mathematics.Random(0x9E3779B9);
            var nextValue = 0;

            for (var step = 0; step < 500; step++)
            {
                var arena = GetBuffer(arenaEntity);
                var dynamic = Manager.GetBuffer<DynamicTestElement>(dynamicEntity);
                var length = dynamic.Length;

                var operation = random.NextInt(0, 8);

                // Operations that need an existing element fall back to Add on an empty buffer.
                if (length == 0 && operation is >= 2 and <= 4)
                {
                    operation = 0;
                }

                switch (operation)
                {
                    case 0:
                    {
                        var value = nextValue++;
                        arena.Add(new ArenaTestElement { Value = value });
                        dynamic.Add(new DynamicTestElement { Value = value });
                        break;
                    }
                    case 1:
                    {
                        var value = nextValue++;
                        var index = random.NextInt(0, length + 1);
                        arena.Insert(index, new ArenaTestElement { Value = value });
                        dynamic.Insert(index, new DynamicTestElement { Value = value });
                        break;
                    }
                    case 2:
                    {
                        var index = random.NextInt(0, length);
                        arena.RemoveAt(index);
                        dynamic.RemoveAt(index);
                        break;
                    }
                    case 3:
                    {
                        var index = random.NextInt(0, length);
                        arena.RemoveAtSwapBack(index);
                        dynamic.RemoveAtSwapBack(index);
                        break;
                    }
                    case 4:
                    {
                        var index = random.NextInt(0, length);
                        var remaining = length - index;
                        var count = remaining <= 1 ? 1 : random.NextInt(1, remaining + 1);
                        arena.RemoveRange(index, count);
                        dynamic.RemoveRange(index, count);
                        break;
                    }
                    case 5:
                    {
                        var newLength = random.NextInt(0, 64);
                        arena.Resize(newLength, NativeArrayOptions.ClearMemory);
                        dynamic.Resize(newLength, NativeArrayOptions.ClearMemory);
                        break;
                    }
                    case 6:
                    {
                        arena.Clear();
                        dynamic.Clear();
                        break;
                    }
                    default:
                    {
                        arena.TrimExcess();
                        dynamic.TrimExcess();
                        break;
                    }
                }

                AssertSameContents(GetBuffer(arenaEntity), Manager.GetBuffer<DynamicTestElement>(dynamicEntity), $"step {step}, operation {operation}");
            }
        }

        private static void AssertSameContents(ArenaBuffer<ArenaTestElement> arena, DynamicBuffer<DynamicTestElement> dynamic, string context)
        {
            Assert.AreEqual(dynamic.Length, arena.Length, $"length diverged after {context}");

            for (var i = 0; i < dynamic.Length; i++)
            {
                Assert.AreEqual(dynamic[i].Value, arena[i].Value, $"element {i} diverged after {context}");
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Growth
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Growing moves the block, and the entity's record is the only thing that knows where the block is.
        /// If the new offset were not written back into chunk memory the next reader would silently address
        /// the freed block - corruption with no exception. This test guards that write back specifically.
        /// </summary>
        [Test]
        public void Growing_WritesTheNewHandleBackIntoChunkMemory()
        {
            var entity = CreateArenaEntity(2);
            UpdateReserveSystem();

            var buffer = GetBuffer(entity);
            buffer.Add(new ArenaTestElement { Value = 10 });
            buffer.Add(new ArenaTestElement { Value = 20 });

            var blockBefore = buffer.Block;
            Assert.AreEqual(2, buffer.Capacity);

            buffer.Add(new ArenaTestElement { Value = 30 });

            var stored = Manager.GetComponentData<ArenaTestElementRef>(entity);
            Assert.AreNotEqual(blockBefore, stored.Block, "the grown block lives somewhere else and the component has to say so");
            Assert.AreEqual(3, stored.Length);
            Assert.AreEqual(4, stored.Capacity);

            var reread = GetBuffer(entity);
            Assert.AreEqual(10, reread[0].Value, "growing must carry the existing elements over");
            Assert.AreEqual(20, reread[1].Value);
            Assert.AreEqual(30, reread[2].Value);
        }

        /// <summary>
        /// TrimExcess is what makes a buffer that spiked and shrank give its memory back. Without it the
        /// arena would keep every buffer at its high water mark forever.
        /// </summary>
        [Test]
        public void TrimExcess_ReturnsTheUnusedSizeClassesToTheArena()
        {
            var entity = CreateArenaEntity(2);
            UpdateReserveSystem();

            var buffer = GetBuffer(entity);
            buffer.Resize(64, NativeArrayOptions.ClearMemory);
            Assert.AreEqual(64, buffer.Capacity);

            var usedAtPeak = Arena->UsedElements;

            buffer.Resize(3, NativeArrayOptions.ClearMemory);
            buffer.TrimExcess();

            Assert.AreEqual(4, GetBuffer(entity).Capacity);
            Assert.Less(Arena->UsedElements, usedAtPeak, "trimming has to actually hand elements back");
        }

        // ------------------------------------------------------------------------------------------------
        // Free lists and locality
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The free list is the reason this design needs no compaction pass: a released block has to come
        /// straight back on the next allocation of its size class. If it did not, an entity churning
        /// population would walk the bump pointer forever and the arena would fragment exactly the way a
        /// pile of DynamicBuffer allocations does.
        /// </summary>
        [Test]
        public void ReleasedBlock_IsReusedByTheNextAllocationOfItsSizeClass()
        {
            var first = CreateArenaEntity(8);
            UpdateReserveSystem();

            var reusedBlock = Manager.GetComponentData<ArenaTestElementRef>(first).Block;
            var pagesAfterFirst = Arena->PageCount;

            ReleaseAllAndDestroy(first);

            var second = CreateArenaEntity(8);
            UpdateReserveSystem();

            Assert.AreEqual(reusedBlock, Manager.GetComponentData<ArenaTestElementRef>(second).Block);
            Assert.AreEqual(pagesAfterFirst, Arena->PageCount, "reusing a free block must not carve a new page");
        }

        /// <summary>
        /// There is no cleanup component, so the destroy pipeline is the only thing standing between a
        /// destroyed entity and a leaked block. Releasing has to happen while the record is still readable.
        /// </summary>
        [Test]
        public void ReleasingBeforeDestroy_ReturnsTheBlockToTheArena()
        {
            var baseline = Arena->UsedElements;

            var entity = CreateArenaEntity(16);
            UpdateReserveSystem();
            Assert.AreEqual(baseline + 16, Arena->UsedElements);

            var freed = ReleaseAllAndDestroy(entity);

            Assert.AreEqual(1, freed, "the pipeline has to report the block it handed back");
            Assert.AreEqual(baseline, Arena->UsedElements);
            Assert.AreEqual(0, Arena->LiveBlocks);
            Assert.IsFalse(Manager.Exists(entity));
        }

        /// <summary>
        /// Releasing the same chunk twice is a realistic pipeline mistake, and a double free would hand the
        /// same offset out to two entities - the worst possible corruption. Released records are stamped
        /// unreserved so the second pass is a no-op.
        /// </summary>
        [Test]
        public void ReleasingTwice_IsANoOp()
        {
            var entity = CreateArenaEntity(16);
            UpdateReserveSystem();

            ref var state = ref World.Unmanaged.GetExistingSystemState<ArenaBufferReserveSystem>();
            var handles = ArenaBufferReleaseHandles.Create(ref state);
            var chunk = Manager.GetChunk(entity);

            Assert.AreEqual(1, handles.ReleaseChunk(chunk));
            Assert.AreEqual(0, handles.ReleaseChunk(chunk), "a released record must not be freed a second time");
            Assert.AreEqual(0, Arena->LiveBlocks);

            handles.Dispose();
        }

        /// <summary>
        /// Removing a record without releasing it is the one unrecoverable user error in this design, and it
        /// is silent by nature - the block simply never comes back. The reserve system has to call it out.
        /// </summary>
        [Test]
        public void RemovingARecordWithoutReleasing_IsReportedAsALeak()
        {
            var entity = CreateArenaEntity(16);
            UpdateReserveSystem();

            Manager.RemoveComponent<ArenaTestElementRef>(entity);

            LogAssert.Expect(LogType.Error, new Regex("ArenaBuffer leak"));
            UpdateReserveSystem();

            Assert.AreEqual(1, Arena->LiveBlocks, "the block really is leaked, the report is not a false alarm");
        }

        /// <summary>
        /// DestroyEntity is enableable, so a chunk the destroy query matches still holds entities that are
        /// staying alive. Releasing the whole chunk would free blocks out from under them - silently, since
        /// the offsets stay readable and simply start aliasing whatever gets the block next. This is the
        /// single most dangerous mistake the release path can make.
        /// </summary>
        [Test]
        public void ReleaseSystem_FreesOnlyTheEntitiesFlaggedForDestruction()
        {
            const int entityCount = 4;

            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            for (var i = 0; i < entityCount; i++)
            {
                entities[i] = CreateArenaEntity(8);
            }

            UpdateReserveSystem();

            for (var i = 0; i < entityCount; i++)
            {
                GetBuffer(entities[i]).Add(new ArenaTestElement { Value = i });
            }

            // All four share a chunk, but only the even ones are doomed.
            for (var i = 0; i < entityCount; i++)
            {
                Manager.AddComponent<DestroyEntity>(entities[i]);
                Manager.SetComponentEnabled<DestroyEntity>(entities[i], i % 2 == 0);
            }

            Assert.AreEqual(entityCount, Arena->LiveBlocks);

            var releaseSystem = World.CreateSystem<ArenaBufferReleaseSystem>();
            releaseSystem.Update(World.Unmanaged);
            World.Unmanaged.ResetUpdateAllocator();

            Assert.AreEqual(entityCount / 2, Arena->LiveBlocks, "only the flagged entities may lose their block");

            for (var i = 0; i < entityCount; i++)
            {
                var refData = Manager.GetComponentData<ArenaTestElementRef>(entities[i]);

                if (i % 2 == 0)
                {
                    Assert.IsFalse(refData.IsReserved, $"entity {i} was flagged and should have been released");
                }
                else
                {
                    Assert.IsTrue(refData.IsReserved, $"entity {i} is staying alive and must keep its block");
                    Assert.AreEqual(i, GetBuffer(entities[i])[0].Value, $"entity {i} lost its data to a neighbour's release");
                }
            }

            entities.Dispose();
        }

        /// <summary>
        /// Whatever the pipeline misses, World teardown has to clean up - and it has to free only this
        /// World's blocks, since the arenas are process global and another World may still be using them.
        /// </summary>
        [Test]
        public void DestroyingAWorld_FreesOnlyThatWorldsBlocks()
        {
            var keptEntity = CreateArenaEntity(16);
            UpdateReserveSystem();

            var otherWorld = new World("Arena Teardown Test World");
            var otherSystem = otherWorld.CreateSystem<ArenaBufferReserveSystem>();

            var otherEntity = otherWorld.EntityManager.CreateEntity();
            otherWorld.EntityManager.AddComponentData(otherEntity, ArenaTestElementRef.Request(32));
            otherSystem.Update(otherWorld.Unmanaged);

            Assert.AreEqual(2, Arena->LiveBlocks);

            otherWorld.Dispose();

            Assert.AreEqual(1, Arena->LiveBlocks, "teardown must free that World's block and leave this World's alone");
            Assert.IsTrue(Manager.GetComponentData<ArenaTestElementRef>(keptEntity).IsReserved);
        }

        /// <summary>
        /// This is the whole reason the feature exists. A DynamicBuffer past its internal capacity gives every
        /// entity its own heap block scattered across the address space; buffers of one element type and size
        /// class have to instead be packed side by side into the same page.
        /// </summary>
        [Test]
        public void BuffersOfOneSizeClass_ArePackedIntoTheSamePage()
        {
            const int entityCount = 256;
            const int capacity = 8;

            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            for (var i = 0; i < entityCount; i++)
            {
                entities[i] = CreateArenaEntity(capacity);
            }

            UpdateReserveSystem();

            // 256 blocks of 8 four byte elements is 8 KB, well inside a single page. Blocks are addresses
            // now rather than packed handles, so packing is measured as a distance from the lowest block
            // handed out - which is the page base, since the carve walks the page back to front.
            var stride = Arena->BlockStride(ArenaAllocator.SizeClassOf(capacity));
            var blocks = new NativeArray<long>(entityCount, Allocator.Temp);
            var lowest = long.MaxValue;

            for (var i = 0; i < entityCount; i++)
            {
                blocks[i] = (long)Manager.GetComponentData<ArenaTestElementRef>(entities[i]).Block;
                lowest = math.min(lowest, blocks[i]);
            }

            var offsets = new NativeHashSet<int>(entityCount, Allocator.Temp);
            var maxOffset = 0;

            for (var i = 0; i < entityCount; i++)
            {
                var byteOffset = (int)(blocks[i] - lowest);

                Assert.IsTrue(byteOffset >= 0 && byteOffset < ArenaAllocator.PageSizeBytes,
                    "every block of this size class fits in one page");
                Assert.IsTrue(offsets.Add(byteOffset), "two entities were handed the same block");
                maxOffset = math.max(maxOffset, byteOffset);
            }

            blocks.Dispose();
            offsets.Dispose();
            entities.Dispose();

            Assert.AreEqual(1, Arena->PageCount);
            Assert.AreEqual((entityCount - 1) * stride, maxOffset, "the blocks have to be packed back to back with no gaps");
            Assert.AreEqual(entityCount * capacity, Arena->UsedElements);
        }

        // ------------------------------------------------------------------------------------------------
        // Isolation between entities
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Neighbouring blocks are adjacent in one allocation, so an off by one in the pointer maths would
        /// corrupt another entity's data rather than crashing. Writing a distinct pattern per entity is what
        /// makes such a bug fail a test instead of shipping.
        /// </summary>
        [Test]
        public void WritesToOneBuffer_DoNotTouchTheNeighbouringBlocks()
        {
            const int entityCount = 32;
            const int elementCount = 4;

            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            for (var i = 0; i < entityCount; i++)
            {
                entities[i] = CreateArenaEntity(elementCount);
            }

            UpdateReserveSystem();

            for (var i = 0; i < entityCount; i++)
            {
                var buffer = GetBuffer(entities[i]);
                for (var e = 0; e < elementCount; e++)
                {
                    buffer.Add(new ArenaTestElement { Value = i * 1000 + e });
                }
            }

            for (var i = 0; i < entityCount; i++)
            {
                var buffer = GetBuffer(entities[i]);
                Assert.AreEqual(elementCount, buffer.Length);

                for (var e = 0; e < elementCount; e++)
                {
                    Assert.AreEqual(i * 1000 + e, buffer[e].Value, $"entity {i} element {e} was overwritten by a neighbour");
                }
            }

            entities.Dispose();
        }

        /// <summary>
        /// Pages are never moved or reallocated, which is what makes it safe to hold a pointer into a block
        /// while unrelated buffers grow. A growth large enough to carve many new pages must leave every other
        /// buffer at the exact same handle, not merely holding the same values.
        /// </summary>
        [Test]
        public void GrowingOneBuffer_LeavesEveryOtherBlockWhereItWas()
        {
            const int entityCount = 64;

            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            for (var i = 0; i < entityCount; i++)
            {
                entities[i] = CreateArenaEntity(4);
            }

            UpdateReserveSystem();

            for (var i = 0; i < entityCount; i++)
            {
                GetBuffer(entities[i]).Add(new ArenaTestElement { Value = i });
            }

            var handlesBefore = new NativeArray<IntPtr>(entityCount, Allocator.Temp);
            for (var i = 0; i < entityCount; i++)
            {
                handlesBefore[i] = GetBuffer(entities[i]).Block;
            }

            var pagesBefore = Arena->PageCount;

            // One buffer balloons far past a page, forcing several new pages to be carved.
            GetBuffer(entities[0]).Resize(ArenaAllocator.PageSizeBytes, NativeArrayOptions.ClearMemory);
            Assert.Greater(Arena->PageCount, pagesBefore, "the arena was expected to have to carve pages");

            for (var i = 1; i < entityCount; i++)
            {
                var buffer = GetBuffer(entities[i]);
                Assert.AreEqual(handlesBefore[i], buffer.Block, $"entity {i} block moved, but pages must never be relocated");
                Assert.AreEqual(1, buffer.Length);
                Assert.AreEqual(i, buffer[0].Value, $"entity {i} lost its data");
            }

            handlesBefore.Dispose();
            entities.Dispose();
        }
    }
}
