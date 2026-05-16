// <copyright project="NZCore.Tests" file="EventBusPerformanceTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.PerformanceTesting;

namespace NZCore.Tests.Events
{
    [Category("Performance")]
    public unsafe class EventBusPerformanceTests
    {
        private struct PerfEvent
        {
            public int Value;
        }

        [BurstCompile]
        private struct WriteJob : IJob
        {
            public EventBus Bus;
            public int Count;

            public void Execute()
            {
                for (var i = 0; i < Count; i++)
                {
                    Bus.Write(new PerfEvent { Value = i });
                }
            }
        }

        [BurstCompile]
        private struct ReadJob : IJob
        {
            public EventBus Bus;
            public NativeReference<int> Sum;

            public void Execute()
            {
                var reader = Bus.AsReader<PerfEvent>();
                var sum = 0;
                while (reader.MoveNext(out var evt))
                {
                    sum += evt.Value;
                }

                Sum.Value = sum;
            }
        }

        [Test]
        [Performance]
        public void Write([Values(100_000, 1_000_000)] int count)
        {
            var bus = EventBus.Create(4, Allocator.Persistent);
            bus.Register<PerfEvent>(count);

            Measure.Method(() => { new WriteJob { Bus = bus, Count = count }.Schedule().Complete(); })
                   .SetUp(() => bus.ClearBus<PerfEvent>())
                   .WarmupCount(1)
                   .MeasurementCount(10)
                   .SampleGroup(new SampleGroup($"EventBus.Write_{count}", SampleUnit.Millisecond))
                   .Run();

            bus.Dispose();
        }

        [Test]
        [Performance]
        public void Read([Values(100_000, 1_000_000)] int count)
        {
            var bus = EventBus.Create(4, Allocator.Persistent);
            bus.Register<PerfEvent>(count);

            var sum = new NativeReference<int>(Allocator.Persistent);

            new WriteJob { Bus = bus, Count = count }.Schedule().Complete();

            Measure.Method(() => { new ReadJob { Bus = bus, Sum = sum }.Schedule().Complete(); })
                   .WarmupCount(1)
                   .MeasurementCount(10)
                   .SampleGroup(new SampleGroup($"EventBus.Read_{count}", SampleUnit.Millisecond))
                   .Run();

            sum.Dispose();
            bus.Dispose();
        }
    }

    [Category("Performance")]
    public unsafe class MixedEventBusPerformanceTests
    {
        private enum PerfEvtType
        {
            A
        }

        private struct PerfPayload
        {
            public int Value;
        }

        [BurstCompile]
        private struct WriteJob : IJob
        {
            public MixedEventBus<PerfEvtType> Bus;
            public int Count;

            public void Execute()
            {
                for (var i = 0; i < Count; i++)
                {
                    Bus.Write(PerfEvtType.A, new PerfPayload { Value = i });
                }
            }
        }

        [BurstCompile]
        private struct ReadJob : IJob
        {
            public MixedEventBus<PerfEvtType> Bus;
            public NativeReference<int> Sum;

            public void Execute()
            {
                var reader = Bus.AsReader();
                var sum = 0;
                while (reader.MoveNext(out _))
                {
                    sum += reader.GetPayload<PerfPayload>().Value;
                }

                Sum.Value = sum;
            }
        }

        [Test]
        [Performance]
        public void Write([Values(100_000, 1_000_000)] int count)
        {
            // MixedEventHeader = 2 ints (8 bytes) + sizeof(PerfPayload) = 4 bytes => 12 bytes/event
            var bus = MixedEventBus<PerfEvtType>.Create(count * 12, Allocator.Persistent);

            Measure.Method(() => { new WriteJob { Bus = bus, Count = count }.Schedule().Complete(); })
                   .SetUp(() => bus.Clear())
                   .WarmupCount(1)
                   .MeasurementCount(10)
                   .SampleGroup(new SampleGroup($"MixedEventBus.Write_{count}", SampleUnit.Millisecond))
                   .Run();

            bus.Dispose();
        }

        [Test]
        [Performance]
        public void Read([Values(100_000, 1_000_000)] int count)
        {
            var bus = MixedEventBus<PerfEvtType>.Create(count * 12, Allocator.Persistent);
            var sum = new NativeReference<int>(Allocator.Persistent);

            new WriteJob { Bus = bus, Count = count }.Schedule().Complete();

            Measure.Method(() => { new ReadJob { Bus = bus, Sum = sum }.Schedule().Complete(); })
                   .WarmupCount(1)
                   .MeasurementCount(10)
                   .SampleGroup(new SampleGroup($"MixedEventBus.Read_{count}", SampleUnit.Millisecond))
                   .Run();

            sum.Dispose();
            bus.Dispose();
        }
    }

    [Category("Performance")]
    public unsafe class ParallelEventBusPerformanceTests
    {
        private struct PerfParallelEvent
        {
            public int Id;
        }

        [BurstCompile]
        private struct ParallelWriteJob : IJobFor
        {
            public UnsafeParallelList<PerfParallelEvent>.ThreadWriter Writer;

            public void Execute(int index)
            {
                Writer.Begin();
                Writer.Write(new PerfParallelEvent { Id = index });
            }
        }

        [BurstCompile]
        private struct ReadJob : IJob
        {
            public UnsafeParallelList<PerfParallelEvent>.ThreadReader Reader;
            public int ThreadCount;
            public NativeReference<int> Sum;

            public void Execute()
            {
                var sum = 0;
                for (var t = 0; t < ThreadCount; t++)
                {
                    var count = Reader.Begin(t);
                    for (var i = 0; i < count; i++)
                    {
                        sum += Reader.Read().Id;
                    }
                }

                Sum.Value = sum;
            }
        }

        [Test]
        [Performance]
        public void Write([Values(100_000, 1_000_000)] int count)
        {
            var bus = ParallelEventBus.Create(4, Allocator.Persistent);
            bus.Register<PerfParallelEvent>(count);

            var writer = bus.AsParallelWriter<PerfParallelEvent>();

            Measure.Method(() =>
                   {
                       new ParallelWriteJob { Writer = writer }
                           .ScheduleParallel(count, 64, default)
                           .Complete();
                   })
                   .SetUp(() => bus.ClearBus<PerfParallelEvent>())
                   .WarmupCount(1)
                   .MeasurementCount(10)
                   .SampleGroup(new SampleGroup($"ParallelEventBus.Write_{count}", SampleUnit.Millisecond))
                   .Run();

            bus.Dispose();
        }

        [Test]
        [Performance]
        public void Read([Values(100_000, 1_000_000)] int count)
        {
            var bus = ParallelEventBus.Create(4, Allocator.Persistent);
            bus.Register<PerfParallelEvent>(count);

            var writer = bus.AsParallelWriter<PerfParallelEvent>();
            var reader = bus.AsParallelReader<PerfParallelEvent>();
            var sum = new NativeReference<int>(Allocator.Persistent);
            var threadCount = JobsUtility.ThreadIndexCount;

            new ParallelWriteJob { Writer = writer }
                .ScheduleParallel(count, 64, default)
                .Complete();

            Measure.Method(() =>
                   {
                       new ReadJob { Reader = reader, ThreadCount = threadCount, Sum = sum }
                           .Schedule()
                           .Complete();
                   })
                   .WarmupCount(1)
                   .MeasurementCount(10)
                   .SampleGroup(new SampleGroup($"ParallelEventBus.Read_{count}", SampleUnit.Millisecond))
                   .Run();

            sum.Dispose();
            bus.Dispose();
        }
    }

    [Category("Performance")]
    public unsafe class UnsafeListBaselinePerformanceTests
    {
        [BurstCompile]
        private struct WriteJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public UnsafeList<int>* List;
            public int Count;

            public void Execute()
            {
                for (var i = 0; i < Count; i++)
                    List->Add(i);
            }
        }

        [BurstCompile]
        private struct ReadJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public UnsafeList<int>* List;
            public NativeReference<int> Sum;

            public void Execute()
            {
                var sum = 0;
                var ptr = List->Ptr;
                var length = List->m_length;
                for (var i = 0; i < length; i++)
                    sum += ptr[i];
                Sum.Value = sum;
            }
        }

        [Test]
        [Performance]
        public void Write([Values(100_000, 1_000_000)] int count)
        {
            var listPtr = UnsafeList<int>.Create(count, Allocator.Persistent);

            Measure.Method(() => { new WriteJob { List = listPtr, Count = count }.Schedule().Complete(); })
                   .SetUp(() => listPtr->Clear())
                   .WarmupCount(1)
                   .MeasurementCount(10)
                   .SampleGroup(new SampleGroup($"UnsafeList.Write_{count}", SampleUnit.Millisecond))
                   .Run();

            listPtr->Dispose();
        }

        [Test]
        [Performance]
        public void Read([Values(100_000, 1_000_000)] int count)
        {
            var listPtr = UnsafeList<int>.Create(count, Allocator.Persistent);
            var sum = new NativeReference<int>(Allocator.Persistent);

            new WriteJob { List = listPtr, Count = count }.Schedule().Complete();

            Measure.Method(() => { new ReadJob { List = listPtr, Sum = sum }.Schedule().Complete(); })
                   .WarmupCount(1)
                   .MeasurementCount(10)
                   .SampleGroup(new SampleGroup($"UnsafeList.Read_{count}", SampleUnit.Millisecond))
                   .Run();

            sum.Dispose();
            listPtr->Dispose();
        }
    }
}