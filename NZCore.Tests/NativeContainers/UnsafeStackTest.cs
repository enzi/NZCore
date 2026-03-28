// <copyright project="NZCore.Tests" file="UnsafeStackTest.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using NUnit.Framework;
using Unity.Collections;
using Unity.PerformanceTesting;

namespace NZCore.Tests
{
    public class UnsafeStackTest
    {
        private const int WarmupCount = 10;
        private const int MeasureCount = 1000;

        [TestCase]
        [Performance]
        public void Test_UnsafeGenericStack()
        {
            UnsafeGenericStack stack = default;

            Measure
                .Method(() =>
                {
                    stack = new UnsafeGenericStack(0, Allocator.Persistent);

                    stack.Push(new TestStruct1()
                    {
                        Val1 = 1,
                        Val2 = 2,
                        Val3 = 3,
                        Val4 = 4,
                        Val5 = 5,
                    });

                    stack.Push(6.0f);
                    stack.Push((double)7.0);
                    stack.Push((int)8);
                    stack.Push((byte)9);

                    stack.Push(new TestStruct1()
                    {
                        Val1 = 1,
                        Val2 = 2,
                        Val3 = 3,
                        Val4 = 4,
                        Val5 = 5,
                    });
                })
                .CleanUp(() =>
                {
                    var valStruct2 = stack.Pop<TestStruct1>();
                    var val9 = stack.Pop<byte>();
                    var val8 = stack.Pop<int>();
                    var val7 = stack.Pop<double>();
                    var val6 = stack.Pop<float>();
                    var valStruct = stack.Pop<TestStruct1>();

                    Assert.AreEqual(1.0f, valStruct.Val1, "val1 not equal");
                    Assert.AreEqual((byte)2, valStruct.Val2, "val2 not equal");
                    Assert.AreEqual(3.0, valStruct.Val3, "val3 not equal");
                    Assert.AreEqual((byte)4, valStruct.Val4, "val4 not equal");
                    Assert.AreEqual(5.0f, valStruct.Val5, "val5 not equal");

                    Assert.AreEqual((float)6.0f, val6, "val6 not equal");
                    Assert.AreEqual((double)7.0, val7, "val7 not equal");
                    Assert.AreEqual((int)8, val8, "val8 not equal");
                    Assert.AreEqual((byte)9, val9, "val9 not equal");

                    Assert.AreEqual(1.0f, valStruct2.Val1, "val1 not equal");
                    Assert.AreEqual((byte)2, valStruct2.Val2, "val2 not equal");
                    Assert.AreEqual(3.0, valStruct2.Val3, "val3 not equal");
                    Assert.AreEqual((byte)4, valStruct2.Val4, "val4 not equal");
                    Assert.AreEqual(5.0f, valStruct2.Val5, "val5 not equal");

                    stack.Dispose();
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasureCount)
                .Run();
        }

        private struct TestStruct1
        {
            public float Val1;
            public byte Val2;
            public double Val3;
            public byte Val4;
            public float Val5;
        }
    }
}