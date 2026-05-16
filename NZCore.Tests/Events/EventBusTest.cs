// <copyright project="NZCore.Tests" file="EventBusTest.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace NZCore.Tests.Events
{
    public unsafe class EventBusTest
    {
        private struct TestEvent
        {
            public int Value;
        }

        private struct AnotherEvent
        {
            public float X;
            public float Y;
        }

        [Test]
        public void Register_And_Write_Count()
        {
            var bus = EventBus.Create(4, Allocator.Temp);
            bus.Register<TestEvent>(16);
            bus.Write(new TestEvent { Value = 1 });
            bus.Write(new TestEvent { Value = 2 });
            bus.Write(new TestEvent { Value = 3 });

            Assert.AreEqual(3, bus.Count<TestEvent>());

            bus.Dispose();
        }

        [Test]
        public void Unregistered_Type_Count_Returns_Zero()
        {
            var bus = EventBus.Create(4, Allocator.Temp);

            Assert.AreEqual(0, bus.Count<TestEvent>());

            bus.Dispose();
        }

        [Test]
        public void Reader_Iterates_Events_In_Order()
        {
            var bus = EventBus.Create(4, Allocator.Temp);
            bus.Register<TestEvent>(16);
            bus.Write(new TestEvent { Value = 10 });
            bus.Write(new TestEvent { Value = 20 });
            bus.Write(new TestEvent { Value = 30 });

            var reader = bus.AsReader<TestEvent>();
            Assert.IsTrue(reader.MoveNext(out var e1));
            Assert.AreEqual(10, e1.Value);
            Assert.IsTrue(reader.MoveNext(out var e2));
            Assert.AreEqual(20, e2.Value);
            Assert.IsTrue(reader.MoveNext(out var e3));
            Assert.AreEqual(30, e3.Value);
            Assert.IsFalse(reader.MoveNext(out _));

            bus.Dispose();
        }

        [Test]
        public void Reader_Count_Matches_Bus_Count()
        {
            var bus = EventBus.Create(4, Allocator.Temp);
            bus.Register<TestEvent>(16);
            bus.Write(new TestEvent { Value = 1 });
            bus.Write(new TestEvent { Value = 2 });

            var reader = bus.AsReader<TestEvent>();
            Assert.AreEqual(bus.Count<TestEvent>(), reader.Count());

            bus.Dispose();
        }

        [Test]
        public void TryGetFirst_Returns_First_Event()
        {
            var bus = EventBus.Create(4, Allocator.Temp);
            bus.Register<TestEvent>(16);
            bus.Write(new TestEvent { Value = 42 });
            bus.Write(new TestEvent { Value = 99 });

            Assert.IsTrue(bus.TryGetFirst(out TestEvent first));
            Assert.AreEqual(42, first.Value);

            bus.Dispose();
        }

        [Test]
        public void TryGetFirst_Empty_Returns_False()
        {
            var bus = EventBus.Create(4, Allocator.Temp);
            bus.Register<TestEvent>(16);

            Assert.IsFalse(bus.TryGetFirst(out TestEvent _));

            bus.Dispose();
        }

        [Test]
        public void ClearBus_Clears_Only_One_Type()
        {
            var bus = EventBus.Create(4, Allocator.Temp);
            bus.Register<TestEvent>(16);
            bus.Register<AnotherEvent>(16);

            bus.Write(new TestEvent { Value = 1 });
            bus.Write(new AnotherEvent { X = 1f, Y = 2f });

            bus.ClearBus<TestEvent>();

            Assert.AreEqual(0, bus.Count<TestEvent>());
            Assert.AreEqual(1, bus.Count<AnotherEvent>());

            bus.Dispose();
        }

        [Test]
        public void Clear_Clears_All_Types()
        {
            var bus = EventBus.Create(4, Allocator.Temp);
            bus.Register<TestEvent>(16);
            bus.Register<AnotherEvent>(16);

            bus.Write(new TestEvent { Value = 1 });
            bus.Write(new AnotherEvent { X = 1f, Y = 2f });

            bus.Clear();

            Assert.AreEqual(0, bus.Count<TestEvent>());
            Assert.AreEqual(0, bus.Count<AnotherEvent>());

            bus.Dispose();
        }

        [Test]
        public void Register_Duplicate_Is_Idempotent()
        {
            var bus = EventBus.Create(4, Allocator.Temp);
            bus.Register<TestEvent>(16);
            bus.Register<TestEvent>(32);

            bus.Write(new TestEvent { Value = 7 });
            Assert.AreEqual(1, bus.Count<TestEvent>());

            bus.Dispose();
        }

        [Test]
        public void Write_Beyond_Initial_Capacity_Grows()
        {
            var bus = EventBus.Create(4, Allocator.Temp);
            bus.Register<TestEvent>(2);

            for (int i = 0; i < 100; i++)
            {
                bus.Write(new TestEvent { Value = i });
            }

            Assert.AreEqual(100, bus.Count<TestEvent>());

            var reader = bus.AsReader<TestEvent>();
            for (int i = 0; i < 100; i++)
            {
                Assert.IsTrue(reader.MoveNext(out var evt));
                Assert.AreEqual(i, evt.Value);
            }

            bus.Dispose();
        }
    }

    public unsafe class MixedEventBusTest
    {
        private enum MyEventType
        {
            TypeA,
            TypeB,
        }

        private struct PayloadA
        {
            public int Value;
        }

        private struct PayloadB
        {
            public float X;
            public float Y;
        }

        [Test]
        public void Write_And_Read_Single_Event()
        {
            var bus = MixedEventBus<MyEventType>.Create(64, Allocator.Temp);
            bus.Write(MyEventType.TypeA, new PayloadA { Value = 42 });

            var reader = bus.AsReader();
            Assert.IsTrue(reader.MoveNext(out var type));
            Assert.AreEqual(MyEventType.TypeA, type);
            Assert.AreEqual(42, reader.GetPayload<PayloadA>().Value);
            Assert.IsFalse(reader.MoveNext(out _));

            bus.Dispose();
        }

        [Test]
        public void Write_Mixed_Types_Read_All_In_Order()
        {
            var bus = MixedEventBus<MyEventType>.Create(64, Allocator.Temp);
            bus.Write(MyEventType.TypeA, new PayloadA { Value = 1 });
            bus.Write(MyEventType.TypeB, new PayloadB { X = 2f, Y = 3f });
            bus.Write(MyEventType.TypeA, new PayloadA { Value = 99 });

            var reader = bus.AsReader();

            Assert.IsTrue(reader.MoveNext(out var t0));
            Assert.AreEqual(MyEventType.TypeA, t0);
            Assert.AreEqual(1, reader.GetPayload<PayloadA>().Value);

            Assert.IsTrue(reader.MoveNext(out var t1));
            Assert.AreEqual(MyEventType.TypeB, t1);
            var pb = reader.GetPayload<PayloadB>();
            Assert.AreEqual(2f, pb.X);
            Assert.AreEqual(3f, pb.Y);

            Assert.IsTrue(reader.MoveNext(out var t2));
            Assert.AreEqual(MyEventType.TypeA, t2);
            Assert.AreEqual(99, reader.GetPayload<PayloadA>().Value);

            Assert.IsFalse(reader.MoveNext(out _));

            bus.Dispose();
        }

        [Test]
        public void Count_Returns_Total_Event_Count()
        {
            var bus = MixedEventBus<MyEventType>.Create(64, Allocator.Temp);
            bus.Write(MyEventType.TypeA, new PayloadA { Value = 1 });
            bus.Write(MyEventType.TypeB, new PayloadB { X = 0f, Y = 0f });
            bus.Write(MyEventType.TypeA, new PayloadA { Value = 2 });

            Assert.AreEqual(3, bus.Count(0));

            bus.Dispose();
        }

        [Test]
        public void Clear_Empties_Bus()
        {
            var bus = MixedEventBus<MyEventType>.Create(64, Allocator.Temp);
            bus.Write(MyEventType.TypeA, new PayloadA { Value = 1 });
            bus.Write(MyEventType.TypeB, new PayloadB { X = 0f, Y = 0f });

            bus.Clear();

            Assert.AreEqual(0, bus.Count(0));
            var reader = bus.AsReader();
            Assert.IsFalse(reader.MoveNext(out _));

            bus.Dispose();
        }
    }

    public unsafe class ParallelEventBusTest
    {
        private struct ParallelEvent
        {
            public int Id;
        }

        private struct OtherParallelEvent
        {
            public long Value;
        }

        [Test]
        public void Register_Write_Count()
        {
            var bus = ParallelEventBus.Create(4, Allocator.Temp);
            bus.Register<ParallelEvent>(16);

            var writer = bus.AsParallelWriter<ParallelEvent>();
            writer.Begin(0);
            writer.Write(new ParallelEvent { Id = 1 });
            writer.Write(new ParallelEvent { Id = 2 });

            Assert.AreEqual(2, bus.Count<ParallelEvent>());

            bus.Dispose();
        }

        [Test]
        public void Reader_Reads_Written_Items()
        {
            var bus = ParallelEventBus.Create(4, Allocator.Temp);
            bus.Register<ParallelEvent>(16);

            var writer = bus.AsParallelWriter<ParallelEvent>();
            writer.Begin(0);
            writer.Write(new ParallelEvent { Id = 10 });
            writer.Write(new ParallelEvent { Id = 20 });

            var reader = bus.AsParallelReader<ParallelEvent>();
            int count = reader.Begin(0);

            Assert.AreEqual(2, count);
            Assert.AreEqual(10, reader.Read().Id);
            Assert.AreEqual(20, reader.Read().Id);

            bus.Dispose();
        }

        [Test]
        public void ClearBus_Clears_One_Type_Leaves_Other()
        {
            var bus = ParallelEventBus.Create(4, Allocator.Temp);
            bus.Register<ParallelEvent>(16);
            bus.Register<OtherParallelEvent>(16);

            var writer1 = bus.AsParallelWriter<ParallelEvent>();
            writer1.Begin(0);
            writer1.Write(new ParallelEvent { Id = 1 });

            var writer2 = bus.AsParallelWriter<OtherParallelEvent>();
            writer2.Begin(0);
            writer2.Write(new OtherParallelEvent { Value = 99 });

            bus.ClearBus<ParallelEvent>();

            Assert.AreEqual(0, bus.Count<ParallelEvent>());
            Assert.AreEqual(1, bus.Count<OtherParallelEvent>());

            bus.Dispose();
        }
    }

    public unsafe class EventBusJobTest
    {
        private struct JobEvent
        {
            public int Value;
        }

        [BurstCompile]
        private struct WriteEventsJob : IJob
        {
            public EventBus Bus;
            public int Count;

            public void Execute()
            {
                for (int i = 0; i < Count; i++)
                    Bus.Write(new JobEvent { Value = i });
            }
        }

        [BurstCompile]
        private struct SumEventsJob : IJob
        {
            public EventBus Bus;
            public NativeReference<int> Sum;

            public void Execute()
            {
                var reader = Bus.AsReader<JobEvent>();
                int sum = 0;
                while (reader.MoveNext(out var evt))
                    sum += evt.Value;
                Sum.Value = sum;
            }
        }

        [Test]
        public void Schedule_Write_Produces_Correct_Count()
        {
            var bus = EventBus.Create(4, Allocator.TempJob);
            bus.Register<JobEvent>(16);

            new WriteEventsJob { Bus = bus, Count = 10 }.Schedule().Complete();

            Assert.AreEqual(10, bus.Count<JobEvent>());

            bus.Dispose();
        }

        [Test]
        public void Schedule_Write_Then_Read_Produces_Correct_Sum()
        {
            var bus = EventBus.Create(4, Allocator.TempJob);
            bus.Register<JobEvent>(16);

            var writeHandle = new WriteEventsJob { Bus = bus, Count = 10 }.Schedule();

            var sum = new NativeReference<int>(Allocator.TempJob);
            new SumEventsJob { Bus = bus, Sum = sum }.Schedule(writeHandle).Complete();

            Assert.AreEqual(45, sum.Value); // 0+1+...+9

            sum.Dispose();
            bus.Dispose();
        }
    }

    public unsafe class MixedEventBusJobTest
    {
        private enum JobEvtType { A, B }

        private struct JobPayloadA { public int Value; }
        private struct JobPayloadB { public float X; }

        [BurstCompile]
        private struct WriteMixedJob : IJob
        {
            public MixedEventBus<JobEvtType> Bus;
            public int Count;

            public void Execute()
            {
                for (int i = 0; i < Count; i++)
                    Bus.Write(JobEvtType.A, new JobPayloadA { Value = i });
                Bus.Write(JobEvtType.B, new JobPayloadB { X = 99f });
            }
        }

        [BurstCompile]
        private struct SumMixedJob : IJob
        {
            public MixedEventBus<JobEvtType> Bus;
            public NativeReference<int> Sum;

            public void Execute()
            {
                var reader = Bus.AsReader();
                int sum = 0;
                while (reader.MoveNext(out var type))
                {
                    if (type == JobEvtType.A)
                        sum += reader.GetPayload<JobPayloadA>().Value;
                }
                Sum.Value = sum;
            }
        }

        [Test]
        public void Schedule_Write_Produces_Correct_Count()
        {
            var bus = MixedEventBus<JobEvtType>.Create(128, Allocator.TempJob);

            new WriteMixedJob { Bus = bus, Count = 5 }.Schedule().Complete();

            Assert.AreEqual(6, bus.Count(0)); // 5x A + 1x B

            bus.Dispose();
        }

        [Test]
        public void Schedule_Write_Then_Read_Sums_Type_A()
        {
            var bus = MixedEventBus<JobEvtType>.Create(128, Allocator.TempJob);

            var writeHandle = new WriteMixedJob { Bus = bus, Count = 5 }.Schedule();

            var sum = new NativeReference<int>(Allocator.TempJob);
            new SumMixedJob { Bus = bus, Sum = sum }.Schedule(writeHandle).Complete();

            Assert.AreEqual(10, sum.Value); // 0+1+2+3+4

            sum.Dispose();
            bus.Dispose();
        }
    }

    public unsafe class ParallelEventBusJobTest
    {
        private struct ParallelJobEvent
        {
            public int Id;
        }

        [BurstCompile]
        private struct ParallelWriteJob : IJobFor
        {
            public UnsafeParallelList<ParallelJobEvent>.ThreadWriter Writer;

            public void Execute(int index)
            {
                Writer.Begin();
                Writer.Write(new ParallelJobEvent { Id = index });
            }
        }

        [BurstCompile]
        private struct SumAllThreadsJob : IJob
        {
            public UnsafeParallelList<ParallelJobEvent>.ThreadReader Reader;
            public int ThreadCount;
            public NativeReference<int> Sum;

            public void Execute()
            {
                int sum = 0;
                for (int t = 0; t < ThreadCount; t++)
                {
                    int count = Reader.Begin(t);
                    for (int i = 0; i < count; i++)
                        sum += Reader.Read().Id;
                }
                Sum.Value = sum;
            }
        }

        [Test]
        public void ScheduleParallel_Write_Produces_Correct_Count()
        {
            var bus = ParallelEventBus.Create(4, Allocator.TempJob);
            bus.Register<ParallelJobEvent>(32);

            var writer = bus.AsParallelWriter<ParallelJobEvent>();
            new ParallelWriteJob { Writer = writer }
                .ScheduleParallel(100, 8, default)
                .Complete();

            Assert.AreEqual(100, bus.Count<ParallelJobEvent>());

            bus.Dispose();
        }

        [Test]
        public void ScheduleParallel_Write_Then_Read_Correct_Sum()
        {
            var bus = ParallelEventBus.Create(4, Allocator.TempJob);
            bus.Register<ParallelJobEvent>(32);

            var writer = bus.AsParallelWriter<ParallelJobEvent>();
            var writeHandle = new ParallelWriteJob { Writer = writer }
                .ScheduleParallel(100, 8, default);

            var sum = new NativeReference<int>(Allocator.TempJob);
            new SumAllThreadsJob
                {
                    Reader = bus.AsParallelReader<ParallelJobEvent>(),
                    ThreadCount = Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndexCount,
                    Sum = sum
                }
                .Schedule(writeHandle)
                .Complete();

            Assert.AreEqual(4950, sum.Value); // 0+1+...+99

            sum.Dispose();
            bus.Dispose();
        }
    }
}
