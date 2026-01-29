// <copyright project="NZCore.Tests" file="DynamicHashMapTest.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NUnit.Framework;
using NZCore.NativeContainers.DenseMap;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.PerformanceTesting;

namespace NZCore.Tests.NativeContainers
{
    

    public unsafe class DynamicHashMapTest : ECSTestsFixture
    {
        private struct TestHashMapBuffer : IDynamicHashMap { public byte Value; }

        private struct SecondHashMapBuffer : IDynamicHashMap { public byte Value; }
        
        private const int WarmupCount = 10;
        private const int MeasureCount = 100;

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [Performance]
        public void Test_DynamicHashMap_Insert(int count)
        {
            var archetype = Manager.CreateArchetype(typeof(TestHashMapBuffer));
            var entity = Manager.CreateEntity(archetype);

            Measure
                .Method(() =>
                {
                    var buffer = Manager.GetBuffer<TestHashMapBuffer>(entity);
                    DynamicHashMap<TestHashMapBuffer, int, int>.Initialize(buffer, (int)(count * 1.5f));
                    var map = buffer.AsHashMap<TestHashMapBuffer, int, int>();

                    for (int i = 0; i < count; i++)
                    {
                        map.TryAdd(i, i);
                    }
                })
                .CleanUp(() =>
                {
                    var buffer = Manager.GetBuffer<TestHashMapBuffer>(entity);
                    var map = buffer.AsHashMap<TestHashMapBuffer, int, int>();

                    // Verify
                    Assert.AreEqual(count, map.Count);
                    for (int i = 0; i < count; i++)
                    {
                        Assert.IsTrue(map.TryGetValue(i, out var val));
                        Assert.AreEqual(i, val);
                    }
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .Run();

            Manager.DestroyEntity(entity);
        }

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [Performance]
        public void Test_DynamicHashMap_Lookup(int count)
        {
            var archetype = Manager.CreateArchetype(typeof(TestHashMapBuffer));
            var entity = Manager.CreateEntity(archetype);
            int foundCount = 0;

            Measure
                .Method(() =>
                {
                    var buffer = Manager.GetBuffer<TestHashMapBuffer>(entity);
                    var map = buffer.AsHashMap<TestHashMapBuffer, int, int>();

                    foundCount = 0;
                    for (int i = 0; i < count; i++)
                    {
                        if (map.TryGetValue(i, out var val) && val == i)
                            foundCount++;
                    }
                })
                .SetUp(() =>
                {
                    var buffer = Manager.GetBuffer<TestHashMapBuffer>(entity);
                    DynamicHashMap<TestHashMapBuffer, int, int>.Initialize(buffer, (int)(count * 1.5f));
                    var map = buffer.AsHashMap<TestHashMapBuffer, int, int>();

                    for (int i = 0; i < count; i++)
                    {
                        map.TryAdd(i, i);
                    }
                })
                .CleanUp(() =>
                {
                    Assert.AreEqual(count, foundCount);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .Run();

            Manager.DestroyEntity(entity);
        }

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [Performance]
        public void Test_DynamicHashMap_InsertJob(int count)
        {
            var archetype = Manager.CreateArchetype(typeof(TestHashMapBuffer));
            var entity = Manager.CreateEntity(archetype);

            Measure
                .Method(() =>
                {
                    var buffer = Manager.GetBuffer<TestHashMapBuffer>(entity);
                    var ptr = (byte*)buffer.GetUnsafePtr();
                    var len = buffer.Length;

                    new DynamicHashMapInsertJob
                    {
                        BufferPtr = ptr,
                        BufferLength = len,
                        Count = count
                    }.Run();
                })
                .SetUp(() =>
                {
                    var buffer = Manager.GetBuffer<TestHashMapBuffer>(entity);
                    DynamicHashMap<TestHashMapBuffer, int, int>.Initialize(buffer, (int)(count * 1.5f));
                })
                .CleanUp(() =>
                {
                    var buffer = Manager.GetBuffer<TestHashMapBuffer>(entity);
                    var map = buffer.AsHashMap<TestHashMapBuffer, int, int>();
                    Assert.AreEqual(count, map.Count);
                    for (int i = 0; i < count; i++)
                    {
                        Assert.IsTrue(map.TryGetValue(i, out var val));
                        Assert.AreEqual(i, val);
                    }
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .Run();

            Manager.DestroyEntity(entity);
        }

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [Performance]
        public void Test_DynamicHashMap_LookupJob(int count)
        {
            var archetype = Manager.CreateArchetype(typeof(TestHashMapBuffer));
            var entity = Manager.CreateEntity(archetype);
            var foundCount = new NativeReference<int>(Allocator.TempJob);

            Measure
                .Method(() =>
                {
                    var buffer = Manager.GetBuffer<TestHashMapBuffer>(entity);
                    var ptr = (byte*)buffer.GetUnsafeReadOnlyPtr();
                    var len = buffer.Length;

                    new DynamicHashMapLookupJob
                    {
                        BufferPtr = ptr,
                        BufferLength = len,
                        Count = count,
                        FoundCount = foundCount
                    }.Run();
                })
                .SetUp(() =>
                {
                    var buffer = Manager.GetBuffer<TestHashMapBuffer>(entity);
                    DynamicHashMap<TestHashMapBuffer, int, int>.Initialize(buffer, (int)(count * 1.5f));
                    var map = buffer.AsHashMap<TestHashMapBuffer, int, int>();

                    for (int i = 0; i < count; i++)
                    {
                        map.TryAdd(i, i);
                    }
                })
                .CleanUp(() =>
                {
                    Assert.AreEqual(count, foundCount.Value);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .Run();

            foundCount.Dispose();
            Manager.DestroyEntity(entity);
        }

        // Comparison tests against UnsafeHashMap
        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [Performance]
        public void Test_UnsafeHashMap_Insert_Comparison(int count)
        {
            UnsafeHashMap<int, int>* map = null;

            Measure
                .Method(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        map->Add(i, i);
                    }
                })
                .SetUp(() =>
                {
                    map = UnsafeCreateHelper.CreateHashMap<int, int>((int)(count * 1.5f), Allocator.Persistent);
                })
                .CleanUp(() =>
                {
                    Assert.AreEqual(count, map->Count);
                    for (int i = 0; i < count; i++)
                    {
                        Assert.IsTrue(map->TryGetValue(i, out var val));
                        Assert.AreEqual(i, val);
                    }
                    map->Dispose();
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .Run();
        }

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [Performance]
        public void Test_UnsafeHashMap_Lookup_Comparison(int count)
        {
            UnsafeHashMap<int, int>* map = null;
            int foundCount = 0;

            Measure
                .Method(() =>
                {
                    foundCount = 0;
                    for (int i = 0; i < count; i++)
                    {
                        if (map->TryGetValue(i, out var val) && val == i)
                            foundCount++;
                    }
                })
                .SetUp(() =>
                {
                    map = UnsafeCreateHelper.CreateHashMap<int, int>((int)(count * 1.5f), Allocator.Persistent);
                    for (int i = 0; i < count; i++)
                    {
                        map->Add(i, i);
                    }
                })
                .CleanUp(() =>
                {
                    Assert.AreEqual(count, foundCount);
                    map->Dispose();
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .Run();
        }

        // Comparison tests against DenseMap
        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [Performance]
        public void Test_DenseMap_Insert_Comparison(int count)
        {
            UnsafeDenseMap<int, int>* map = null;

            Measure
                .Method(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        map->Emplace(i, i);
                    }
                })
                .SetUp(() =>
                {
                    map = UnsafeDenseMap<int, int>.Create((uint)(count * 1.5f), 0.9, Allocator.Persistent);
                })
                .CleanUp(() =>
                {
                    Assert.AreEqual(count, map->Count);
                    for (int i = 0; i < count; i++)
                    {
                        Assert.IsTrue(map->Get(i, out var val));
                        Assert.AreEqual(i, val);
                    }
                    map->Dispose();
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .Run();
        }

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [Performance]
        public void Test_DenseMap_Lookup_Comparison(int count)
        {
            UnsafeDenseMap<int, int>* map = null;
            int foundCount = 0;

            Measure
                .Method(() =>
                {
                    foundCount = 0;
                    for (int i = 0; i < count; i++)
                    {
                        if (map->Get(i, out var val) && val == i)
                            foundCount++;
                    }
                })
                .SetUp(() =>
                {
                    map = UnsafeDenseMap<int, int>.Create((uint)(count * 1.5f), 0.9, Allocator.Persistent);
                    for (int i = 0; i < count; i++)
                    {
                        map->Emplace(i, i);
                    }
                })
                .CleanUp(() =>
                {
                    Assert.AreEqual(count, foundCount);
                    map->Dispose();
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .Run();
        }

        // Correctness tests
        [Test]
        public void Test_DynamicHashMap_Correctness()
        {
            var archetype = Manager.CreateArchetype(typeof(TestHashMapBuffer));
            var entity = Manager.CreateEntity(archetype);
            var buffer = Manager.GetBuffer<TestHashMapBuffer>(entity);

            DynamicHashMap<TestHashMapBuffer, int, int>.Initialize(buffer, 32);
            var map = buffer.AsHashMap<TestHashMapBuffer, int, int>();

            // Test TryAdd
            Assert.IsTrue(map.TryAdd(1, 100));
            Assert.IsTrue(map.TryAdd(2, 200));
            Assert.IsFalse(map.TryAdd(1, 999)); // Duplicate
            Assert.AreEqual(2, map.Count);

            // Test TryGetValue
            Assert.IsTrue(map.TryGetValue(1, out var val1));
            Assert.AreEqual(100, val1);
            Assert.IsTrue(map.TryGetValue(2, out var val2));
            Assert.AreEqual(200, val2);
            Assert.IsFalse(map.TryGetValue(999, out _));

            // Test ContainsKey
            Assert.IsTrue(map.ContainsKey(1));
            Assert.IsTrue(map.ContainsKey(2));
            Assert.IsFalse(map.ContainsKey(999));

            // Test indexer set (update)
            map[1] = 111;
            Assert.AreEqual(111, map[1]);

            // Test Remove
            Assert.IsTrue(map.Remove(1));
            Assert.IsFalse(map.ContainsKey(1));
            Assert.AreEqual(1, map.Count);
            Assert.IsFalse(map.Remove(1)); // Already removed

            // Test reinsert after remove
            Assert.IsTrue(map.TryAdd(1, 1000));
            Assert.AreEqual(1000, map[1]);

            // Test Clear
            map.Clear();
            Assert.AreEqual(0, map.Count);
            Assert.IsFalse(map.ContainsKey(1));
            Assert.IsFalse(map.ContainsKey(2));

            Manager.DestroyEntity(entity);
        }

        [Test]
        public void Test_DynamicHashMap_TryGetRefValue()
        {
            var archetype = Manager.CreateArchetype(typeof(TestHashMapBuffer));
            var entity = Manager.CreateEntity(archetype);
            var buffer = Manager.GetBuffer<TestHashMapBuffer>(entity);

            DynamicHashMap<TestHashMapBuffer, int, int>.Initialize(buffer, 32);
            var map = buffer.AsHashMap<TestHashMapBuffer, int, int>();

            map.TryAdd(1, 100);

            Assert.IsTrue(map.TryGetRefValue(1, out var ptr));
            Assert.AreEqual(100, *ptr);

            // Modify through pointer
            *ptr = 999;
            Assert.AreEqual(999, map[1]);

            // Non-existent key
            Assert.IsFalse(map.TryGetRefValue(999, out var nullPtr));
            Assert.IsTrue(nullPtr == null);

            Manager.DestroyEntity(entity);
        }

        [Test]
        public void Test_DynamicHashMap_MultipleBuffersPerEntity()
        {
            var archetype = Manager.CreateArchetype(typeof(TestHashMapBuffer), typeof(SecondHashMapBuffer));
            var entity = Manager.CreateEntity(archetype);

            var buffer1 = Manager.GetBuffer<TestHashMapBuffer>(entity);
            var buffer2 = Manager.GetBuffer<SecondHashMapBuffer>(entity);

            DynamicHashMap<TestHashMapBuffer, int, int>.Initialize(buffer1, 32);
            DynamicHashMap<SecondHashMapBuffer, int, float>.Initialize(buffer2, 32);

            var map1 = buffer1.AsHashMap<TestHashMapBuffer, int, int>();
            var map2 = buffer2.AsHashMap<SecondHashMapBuffer, int, float>();

            map1.TryAdd(1, 100);
            map2.TryAdd(1, 3.14f);

            Assert.AreEqual(100, map1[1]);
            Assert.AreEqual(3.14f, map2[1], 0.001f);

            // They are independent
            map1.Remove(1);
            Assert.IsFalse(map1.ContainsKey(1));
            Assert.IsTrue(map2.ContainsKey(1));

            Manager.DestroyEntity(entity);
        }

        [Test]
        public void Test_DynamicHashMap_HighLoadFactor()
        {
            var archetype = Manager.CreateArchetype(typeof(TestHashMapBuffer));
            var entity = Manager.CreateEntity(archetype);
            var buffer = Manager.GetBuffer<TestHashMapBuffer>(entity);

            const int capacity = 64;
            const int count = 50; // ~78% load factor

            DynamicHashMap<TestHashMapBuffer, int, int>.Initialize(buffer, capacity);
            var map = buffer.AsHashMap<TestHashMapBuffer, int, int>();

            for (int i = 0; i < count; i++)
            {
                Assert.IsTrue(map.TryAdd(i * 7, i), $"Failed to add key {i * 7}"); // Spread keys
            }

            Assert.AreEqual(count, map.Count);

            for (int i = 0; i < count; i++)
            {
                Assert.IsTrue(map.TryGetValue(i * 7, out var val), $"Failed to get key {i * 7}");
                Assert.AreEqual(i, val);
            }

            Manager.DestroyEntity(entity);
        }

        [Test]
        public void Test_DynamicHashMap_Enumerator()
        {
            var archetype = Manager.CreateArchetype(typeof(TestHashMapBuffer));
            var entity = Manager.CreateEntity(archetype);
            var buffer = Manager.GetBuffer<TestHashMapBuffer>(entity);

            DynamicHashMap<TestHashMapBuffer, int, int>.Initialize(buffer, 32);
            var map = buffer.AsHashMap<TestHashMapBuffer, int, int>();

            map.TryAdd(1, 100);
            map.TryAdd(2, 200);
            map.TryAdd(3, 300);

            int sum = 0;
            int keySum = 0;
            int enumCount = 0;

            foreach (var (key, value) in map)
            {
                keySum += key;
                sum += value;
                enumCount++;
            }

            Assert.AreEqual(3, enumCount);
            Assert.AreEqual(6, keySum);   // 1 + 2 + 3
            Assert.AreEqual(600, sum);    // 100 + 200 + 300

            Manager.DestroyEntity(entity);
        }

        [BurstCompile]
        private struct DynamicHashMapInsertJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public byte* BufferPtr;
            public int BufferLength;
            public int Count;

            public void Execute()
            {
                var map = new DynamicHashMap<TestHashMapBuffer, int, int>(BufferPtr, BufferLength);

                for (int i = 0; i < Count; i++)
                {
                    map.TryAdd(i, i);
                }
            }
        }

        [BurstCompile]
        private struct DynamicHashMapLookupJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public byte* BufferPtr;
            public int BufferLength;
            public int Count;
            public NativeReference<int> FoundCount;

            public void Execute()
            {
                var map = new DynamicHashMap<TestHashMapBuffer, int, int>(BufferPtr, BufferLength);

                int found = 0;
                for (int i = 0; i < Count; i++)
                {
                    if (map.TryGetValue(i, out var val) && val == i)
                        found++;
                }
                FoundCount.Value = found;
            }
        }
    }
}