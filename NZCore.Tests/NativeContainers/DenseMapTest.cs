using System.Runtime.CompilerServices;
using NUnit.Framework;
using NZCore.NativeContainers.DenseMap;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.PerformanceTesting;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace NZCore.Tests
{
    public unsafe class DenseMapTest
    {
        private int warmupCount = 10;
        private int measureCount = 1000;

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [TestCase(1000000)]
        [Performance]
        public void Test_DenseMap(int count)
        {
            UnsafeDenseMap<int, int>* map = null;

            Measure
                .Method(() =>
                {
                    map = UnsafeDenseMap<int, int>.Create((uint)count, 0.9, Allocator.Persistent);

                    int key = 0;
                    int value = 0;
                    for (int i = 0; i < count; i++)
                    {
                        map->Emplace(key, value);

                        key++;
                        value++;
                    }
                })
                .CleanUp(() =>
                {
                    int key = 50;
                    int value = 50;

                    for (int i = 50; i < count; i++)
                    {
                        map->Get(key, out var val);

                        Assert.AreEqual(val, value, "val not equal");

                        key++;
                        value++;
                    }

                    map->Dispose();
                })
                .WarmupCount(warmupCount)
                .MeasurementCount(measureCount)
                .Run();
        }

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [TestCase(1000000)]
        [Performance]
        public void Test_DenseMapDisposeTest(int count)
        {
            Measure
                .Method(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        var map = UnsafeDenseMap<int, int>.Create(100, 0.9, Allocator.Persistent);
                        map->Dispose();
                    }
                })
                .WarmupCount(1)
                .MeasurementCount(1)
                .Run();
        }

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [TestCase(1000000)]
        [Performance]
        public void Test_DenseMapInsertJob(int count)
        {
            UnsafeDenseMap<int, int>* map = null;

            Measure
                .Method(() =>
                {
                    var handle = new DenseMapInsertTestJob()
                    {
                        Count = (uint)count,
                        Map = map
                    }.Schedule();

                    handle.Complete();
                })
                .CleanUp(() => { map->Dispose(); })
                .SetUp(() => { map = UnsafeDenseMap<int, int>.Create((uint)(count * 1.2), 0.9, Allocator.Persistent); })
                .WarmupCount(warmupCount) // 10
                .MeasurementCount(measureCount) // 1000
                .Run();
        }

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [TestCase(1000000)]
        [Performance]
        public void Test_DenseMapLookupJob(int count)
        {
            UnsafeDenseMap<int, int>* map = null;

            Measure
                .Method(() =>
                {
                    var handle = new DenseMapLookupTestJob()
                    {
                        Count = (uint)count,
                        Map = map
                    }.Schedule();

                    handle.Complete();
                })
                .SetUp(() =>
                {
                    map = UnsafeDenseMap<int, int>.Create((uint)(count * 1.2), 0.9, Allocator.Persistent);

                    new DenseMapInsertTestJob()
                    {
                        Count = (uint)count,
                        Map = map
                    }.Run();
                })
                .CleanUp(() => { map->Dispose(); })
                .WarmupCount(warmupCount)
                .MeasurementCount(measureCount)
                .Run();
        }

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [TestCase(1000000)]
        [Performance]
        public void Test_NativeHashMapInsertJob(int count)
        {
            UnsafeHashMap<int, int>* map = null;

            Measure
                .Method(() =>
                {
                    var jobHandle = new NativeHashInsertMapTestJob()
                    {
                        Count = (uint)count,
                        Map = map
                    }.Schedule();

                    jobHandle.Complete();
                })
                .CleanUp(() => { map->Dispose(); })
                .SetUp(() => { map = UnsafeCreateHelper.CreateHashMap<int, int>(count, Allocator.Persistent); })
                .WarmupCount(warmupCount)
                .MeasurementCount(measureCount)
                .Run();
        }

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [TestCase(1000000)]
        [Performance]
        public void Test_NativeHashMapLookupJob(int count)
        {
            UnsafeHashMap<int, int>* map = null;

            Measure
                .Method(() =>
                {
                    var handle = new NativeHashMapLookupTestJob()
                    {
                        Count = (uint)count,
                        Map = map
                    }.Schedule();

                    handle.Complete();
                })
                .SetUp(() =>
                {
                    map = UnsafeCreateHelper.CreateHashMap<int, int>(count, Allocator.Persistent);

                    new NativeHashInsertMapTestJob()
                    {
                        Count = (uint)count,
                        Map = map
                    }.Run();
                })
                .CleanUp(() => { map->Dispose(); })
                .WarmupCount(warmupCount)
                .MeasurementCount(measureCount)
                .Run();
        }

        [BurstCompile]
        private struct DenseMapInsertTestJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public UnsafeDenseMap<int, int>* Map;
            public uint Count;
            public Random rng;

            public void Execute()
            {
                int key = 0;
                int value = 0;

                for (int i = 0; i < Count; i++)
                {
                    Map->Emplace(key, value);

                    key++;
                    value++;
                }
            }
        }

        [BurstCompile]
        private struct NativeHashInsertMapTestJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public UnsafeHashMap<int, int>* Map;
            public uint Count;

            public void Execute()
            {
                int key = 0;
                int value = 0;

                for (int i = 0; i < Count; i++)
                {
                    Map->Add(key, value);

                    key++;
                    value++;
                }
            }
        }

        [BurstCompile]
        private struct DenseMapLookupTestJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public UnsafeDenseMap<int, int>* Map;
            public uint Count;

            //[MethodImpl(MethodImplOptions.NoInlining)]
            public void Execute()
            {
                int key = 0;

                for (int i = 0; i < Count; i++)
                {
                    Map->Get(key, out var value);
                    key++;

                    if (value == -1)
                        Debug.Log("Found");
                }
            }
        }

        [BurstCompile]
        private struct NativeHashMapLookupTestJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public UnsafeHashMap<int, int>* Map;
            public uint Count;

            public void Execute()
            {
                int key = 0;

                for (int i = 0; i < Count; i++)
                {
                    var value = Map[0][key];

                    key++;

                    if (value == -1)
                        Debug.Log("Found");
                }
            }
        }
    }
}