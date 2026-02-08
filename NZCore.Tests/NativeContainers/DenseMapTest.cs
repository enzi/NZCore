// <copyright project="NZCore.Tests" file="DenseMapTest.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NUnit.Framework;
using NZCore.NativeContainers.DenseMap;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.PerformanceTesting;
using Random = Unity.Mathematics.Random;

namespace NZCore.Tests.NativeContainers
{
    public unsafe class DenseMapTest
    {
        #region Validity Tests

        [Test]
        public void Emplace_And_Get_Works()
        {
            var map = UnsafeDenseMap<int, int>.Create(16, 0.9, Allocator.Temp);

            map->Emplace(1, 100);
            map->Emplace(2, 200);
            map->Emplace(3, 300);

            Assert.AreEqual(3, map->Count);

            Assert.IsTrue(map->Get(1, out var val1));
            Assert.AreEqual(100, val1);

            Assert.IsTrue(map->Get(2, out var val2));
            Assert.AreEqual(200, val2);

            Assert.IsTrue(map->Get(3, out var val3));
            Assert.AreEqual(300, val3);

            map->Dispose();
        }

        [Test]
        public void Get_NonExistent_ReturnsFalse()
        {
            var map = UnsafeDenseMap<int, int>.Create(16, 0.9, Allocator.Temp);

            map->Emplace(1, 100);

            Assert.IsFalse(map->Get(999, out _));

            map->Dispose();
        }

        [Test]
        public void Emplace_Duplicate_ReturnsFalse()
        {
            var map = UnsafeDenseMap<int, int>.Create(16, 0.9, Allocator.Temp);

            Assert.IsTrue(map->Emplace(1, 100));
            Assert.IsFalse(map->Emplace(1, 200)); // duplicate

            Assert.AreEqual(1, map->Count);

            map->Get(1, out var val);
            Assert.AreEqual(100, val); // original value preserved

            map->Dispose();
        }

        [Test]
        public void Resize_PreservesData()
        {
            // Start small to force resize
            var map = UnsafeDenseMap<int, int>.Create(16, 0.9, Allocator.Temp);

            // Add enough to trigger resize
            for (int i = 0; i < 100; i++)
            {
                map->Emplace(i, i * 10);
            }

            Assert.AreEqual(100, map->Count);

            // Verify all data is still accessible
            for (int i = 0; i < 100; i++)
            {
                Assert.IsTrue(map->Get(i, out var val), $"Key {i} not found after resize");
                Assert.AreEqual(i * 10, val);
            }

            map->Dispose();
        }

        [Test]
        public void StressTest_ManyInserts()
        {
            var map = UnsafeDenseMap<int, int>.Create(1000, 0.9, Allocator.Temp);

            for (int i = 0; i < 10000; i++)
            {
                map->Emplace(i, i);
            }

            Assert.AreEqual(10000, map->Count);

            // Verify random samples
            var random = new Random(42);
            for (int i = 0; i < 100; i++)
            {
                int key = random.NextInt(0, 10000);
                Assert.IsTrue(map->Get(key, out var val));
                Assert.AreEqual(key, val);
            }

            map->Dispose();
        }

        #endregion

        #region Burst Jobs

        [BurstCompile]
        private struct DenseMapInsertJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public UnsafeDenseMap<int, int>* Map;
            public int Count;

            public void Execute()
            {
                for (int i = 0; i < Count; i++)
                {
                    Map->Emplace(i, i);
                }
            }
        }

        [BurstCompile]
        private struct DenseMapSequentialLookupJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] [ReadOnly] public UnsafeDenseMap<int, int>* Map;
            public int Count;
            public NativeReference<long> Sum;

            public void Execute()
            {
                long sum = 0;
                for (int i = 0; i < Count; i++)
                {
                    Map->Get(i, out var value);
                    sum += value;
                }
                Sum.Value = sum;
            }
        }

        [BurstCompile]
        private struct DenseMapRandomLookupJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] [ReadOnly] public UnsafeDenseMap<int, int>* Map;
            [ReadOnly] public NativeArray<int> RandomKeys;
            public NativeReference<long> Sum;

            public void Execute()
            {
                long sum = 0;
                for (int i = 0; i < RandomKeys.Length; i++)
                {
                    Map->Get(RandomKeys[i], out var value);
                    sum += value;
                }
                Sum.Value = sum;
            }
        }

        [BurstCompile]
        private struct HashMapInsertJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public UnsafeHashMap<int, int>* Map;
            public int Count;

            public void Execute()
            {
                for (int i = 0; i < Count; i++)
                {
                    Map->Add(i, i);
                }
            }
        }

        [BurstCompile]
        private struct HashMapSequentialLookupJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] [ReadOnly] public UnsafeHashMap<int, int>* Map;
            public int Count;
            public NativeReference<long> Sum;

            public void Execute()
            {
                long sum = 0;
                for (int i = 0; i < Count; i++)
                {
                    sum += (*Map)[i];
                }
                Sum.Value = sum;
            }
        }

        [BurstCompile]
        private struct HashMapRandomLookupJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] [ReadOnly] public UnsafeHashMap<int, int>* Map;
            [ReadOnly] public NativeArray<int> RandomKeys;
            public NativeReference<long> Sum;

            public void Execute()
            {
                long sum = 0;
                for (int i = 0; i < RandomKeys.Length; i++)
                {
                    sum += (*Map)[RandomKeys[i]];
                }
                Sum.Value = sum;
            }
        }

        #endregion

        #region Performance Tests

        private const int WarmupCount = 3;
        private const int MeasurementCount = 10;
        private const int IterationsPerMeasurement = 1;
        private const int ElementCount = 100000;

        [Test, Performance]
        public void Perf_Insert_DenseMap()
        {
            UnsafeDenseMap<int, int>* map = null;

            var job = new DenseMapInsertJob { Count = ElementCount };

            Measure.Method(() =>
                {
                    job.Map = map;
                    job.Run();
                })
                .SetUp(() => { map = UnsafeDenseMap<int, int>.Create((uint)(ElementCount * 1.2), 0.9, Allocator.Persistent); })
                .CleanUp(() => { map->Dispose(); })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(IterationsPerMeasurement)
                .Run();
        }

        [Test, Performance]
        public void Perf_Insert_UnsafeHashMap()
        {
            UnsafeHashMap<int, int>* map = null;

            var job = new HashMapInsertJob { Count = ElementCount };

            Measure.Method(() =>
                {
                    job.Map = map;
                    job.Run();
                })
                .SetUp(() => { map = UnsafeCreateHelper.CreateHashMap<int, int>(ElementCount, Allocator.Persistent); })
                .CleanUp(() => { map->Dispose(); })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(IterationsPerMeasurement)
                .Run();
        }

        [Test, Performance]
        public void Perf_SequentialLookup_DenseMap()
        {
            var map = UnsafeDenseMap<int, int>.Create((uint)(ElementCount * 1.2), 0.9, Allocator.Persistent);
            var sum = new NativeReference<long>(Allocator.Persistent);

            new DenseMapInsertJob { Map = map, Count = ElementCount }.Run();

            var job = new DenseMapSequentialLookupJob { Map = map, Count = ElementCount, Sum = sum };

            Measure.Method(() => job.Run())
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(IterationsPerMeasurement)
                .Run();

            Assert.Greater(sum.Value, 0);

            map->Dispose();
            sum.Dispose();
        }

        [Test, Performance]
        public void Perf_SequentialLookup_UnsafeHashMap()
        {
            var map = UnsafeCreateHelper.CreateHashMap<int, int>(ElementCount, Allocator.Persistent);
            var sum = new NativeReference<long>(Allocator.Persistent);

            new HashMapInsertJob { Map = map, Count = ElementCount }.Run();

            var job = new HashMapSequentialLookupJob { Map = map, Count = ElementCount, Sum = sum };

            Measure.Method(() => job.Run())
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(IterationsPerMeasurement)
                .Run();

            Assert.Greater(sum.Value, 0);

            map->Dispose();
            sum.Dispose();
        }

        [Test, Performance]
        public void Perf_RandomLookup_DenseMap()
        {
            var map = UnsafeDenseMap<int, int>.Create((uint)(ElementCount * 1.2), 0.9, Allocator.Persistent);
            var randomKeys = new NativeArray<int>(ElementCount, Allocator.Persistent);
            var sum = new NativeReference<long>(Allocator.Persistent);

            new DenseMapInsertJob { Map = map, Count = ElementCount }.Run();

            // Pre-generate random keys
            var random = new Random(42);
            for (int i = 0; i < ElementCount; i++)
            {
                randomKeys[i] = random.NextInt(0, ElementCount);
            }

            var job = new DenseMapRandomLookupJob { Map = map, RandomKeys = randomKeys, Sum = sum };

            Measure.Method(() => job.Run())
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(IterationsPerMeasurement)
                .Run();

            Assert.Greater(sum.Value, 0);

            map->Dispose();
            randomKeys.Dispose();
            sum.Dispose();
        }

        [Test, Performance]
        public void Perf_RandomLookup_UnsafeHashMap()
        {
            var map = UnsafeCreateHelper.CreateHashMap<int, int>(ElementCount, Allocator.Persistent);
            var randomKeys = new NativeArray<int>(ElementCount, Allocator.Persistent);
            var sum = new NativeReference<long>(Allocator.Persistent);

            new HashMapInsertJob { Map = map, Count = ElementCount }.Run();

            // Pre-generate random keys (same seed for fair comparison)
            var random = new Random(42);
            for (int i = 0; i < ElementCount; i++)
            {
                randomKeys[i] = random.NextInt(0, ElementCount);
            }

            var job = new HashMapRandomLookupJob { Map = map, RandomKeys = randomKeys, Sum = sum };

            Measure.Method(() => job.Run())
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(IterationsPerMeasurement)
                .Run();

            Assert.Greater(sum.Value, 0);

            map->Dispose();
            randomKeys.Dispose();
            sum.Dispose();
        }

        #endregion

        #region Scalability Tests

        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [TestCase(1000000)]
        [Performance]
        public void Perf_Insert_DenseMap_Scaled(int count)
        {
            UnsafeDenseMap<int, int>* map = null;

            Measure.Method(() =>
                {
                    new DenseMapInsertJob { Map = map, Count = count }.Run();
                })
                .SetUp(() => { map = UnsafeDenseMap<int, int>.Create((uint)(count * 1.2), 0.9, Allocator.Persistent); })
                .CleanUp(() => { map->Dispose(); })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(IterationsPerMeasurement)
                .Run();
        }

        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [TestCase(1000000)]
        [Performance]
        public void Perf_Insert_UnsafeHashMap_Scaled(int count)
        {
            UnsafeHashMap<int, int>* map = null;

            Measure.Method(() =>
                {
                    new HashMapInsertJob { Map = map, Count = count }.Run();
                })
                .SetUp(() => { map = UnsafeCreateHelper.CreateHashMap<int, int>(count, Allocator.Persistent); })
                .CleanUp(() => { map->Dispose(); })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(IterationsPerMeasurement)
                .Run();
        }

        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [TestCase(1000000)]
        [Performance]
        public void Perf_SequentialLookup_DenseMap_Scaled(int count)
        {
            var map = UnsafeDenseMap<int, int>.Create((uint)(count * 1.2), 0.9, Allocator.Persistent);
            var sum = new NativeReference<long>(Allocator.Persistent);

            new DenseMapInsertJob { Map = map, Count = count }.Run();

            var job = new DenseMapSequentialLookupJob { Map = map, Count = count, Sum = sum };

            Measure.Method(() => job.Run())
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(IterationsPerMeasurement)
                .Run();

            map->Dispose();
            sum.Dispose();
        }

        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [TestCase(1000000)]
        [Performance]
        public void Perf_SequentialLookup_UnsafeHashMap_Scaled(int count)
        {
            var map = UnsafeCreateHelper.CreateHashMap<int, int>(count, Allocator.Persistent);
            var sum = new NativeReference<long>(Allocator.Persistent);

            new HashMapInsertJob { Map = map, Count = count }.Run();

            var job = new HashMapSequentialLookupJob { Map = map, Count = count, Sum = sum };

            Measure.Method(() => job.Run())
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(IterationsPerMeasurement)
                .Run();

            map->Dispose();
            sum.Dispose();
        }

        #endregion
    }

    internal static unsafe class UnsafeCreateHelper
    {
        public static UnsafeHashMap<TKey, TValue>* CreateHashMap<TKey, TValue>(int capacity, Allocator allocator)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var map = (UnsafeHashMap<TKey, TValue>*)UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<UnsafeHashMap<TKey, TValue>>(),
                UnsafeUtility.AlignOf<UnsafeHashMap<TKey, TValue>>(),
                allocator);
            *map = new UnsafeHashMap<TKey, TValue>(capacity, allocator);
            return map;
        }
    }
}