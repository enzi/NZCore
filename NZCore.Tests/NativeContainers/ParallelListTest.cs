// <copyright project="NZCore.Tests" file="ParallelListTest.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.PerformanceTesting;

namespace NZCore.Tests.NativeContainers
{
    public class ParallelListTest
    {
        public struct PerformanceTestStruct2
        {
            public int data1;
            public int data2;
            public float data3;
        }
        
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [TestCase(250000)]
        [TestCase(1000000)]
        [Performance]
        public void Test_ParallelListCopyToArraySingle(int writeCount)
        {
            ParallelList<PerformanceTestStruct2> parallelList = default;
            NativeList<PerformanceTestStruct2> nativeList = default;

            int warmupCount = 50;
            int measureCount = 250;
            
            int testDone = 0;
            
            Measure
                .Method(() =>
                {
                    //Debug.Log("Method called");

                    var jobHandle = parallelList.CopyToArraySingle(ref nativeList, default);
                    jobHandle.Complete();
                    
                    nativeList.Clear();
                })
                .SetUp(() =>
                {
                    if (testDone == 0)
                    {
                        //Debug.Log("setup called");
                        parallelList = new ParallelList<PerformanceTestStruct2>(0, Allocator.Persistent);
                        
                        var writeJobHandle = new TestJob_ParallelListWrite()
                        {
                            WriteCount = writeCount,
                            ParallelList = parallelList.AsThreadWriter(),
                        }.ScheduleParallel(JobsUtility.ThreadIndexCount, 1, default);
                        writeJobHandle.Complete();
                        
                        nativeList = new NativeList<PerformanceTestStruct2>(parallelList.Length, Allocator.TempJob);
                    }
                })
                .CleanUp(() =>
                {
                    testDone++;

                    if (testDone == warmupCount + measureCount) // on the last test
                    {
                        //Debug.Log("cleanup called");
                        parallelList.Dispose();
                    }
                })
                .WarmupCount(warmupCount)
                .MeasurementCount(measureCount)
                .Run();
        }

        [BurstCompile]
        public struct TestJob_ParallelListWrite : IJobFor
        {
            public int WriteCount;
            public ParallelList<PerformanceTestStruct2>.ThreadWriter ParallelList;
            
            public void Execute(int index)
            {
                ParallelList.Begin();
                
                for (int i = 0; i < WriteCount;i++)
                {
                    ParallelList.Write(new PerformanceTestStruct2()
                    {
                        data1 = 1,
                        data2 = 2,
                        data3 = 3.0f
                    });
                }
            }
        }
    }
}