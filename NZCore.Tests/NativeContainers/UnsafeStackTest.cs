using NUnit.Framework;
using Unity.Collections;
using Unity.PerformanceTesting;

namespace NZCore.Tests
{
    public class UnsafeStackTest
    {
        private int warmupCount = 10;
        private int measureCount = 1000;
        
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
                        val1 = 1,
                        val2 = 2,
                        val3 = 3,
                        val4 = 4,
                        val5 = 5,
                    });
                    
                    stack.Push(6.0f);
                    stack.Push((double) 7.0);
                    stack.Push((int) 8);
                    stack.Push((byte) 9);
                    
                    stack.Push(new TestStruct1()
                    {
                        val1 = 1,
                        val2 = 2,
                        val3 = 3,
                        val4 = 4,
                        val5 = 5,
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
                    
                    Assert.AreEqual(1.0f,       valStruct.val1, "val1 not equal");
                    Assert.AreEqual((byte) 2,   valStruct.val2, "val2 not equal");
                    Assert.AreEqual(3.0,        valStruct.val3, "val3 not equal");
                    Assert.AreEqual((byte) 4,   valStruct.val4, "val4 not equal");
                    Assert.AreEqual(5.0f,       valStruct.val5, "val5 not equal");
                    
                    Assert.AreEqual((float) 6.0f,   val6, "val6 not equal");
                    Assert.AreEqual((double) 7.0,   val7, "val7 not equal");
                    Assert.AreEqual((int) 8,        val8, "val8 not equal");
                    Assert.AreEqual((byte) 9,       val9, "val9 not equal");
                    
                    Assert.AreEqual(1.0f,       valStruct2.val1, "val1 not equal");
                    Assert.AreEqual((byte) 2,   valStruct2.val2, "val2 not equal");
                    Assert.AreEqual(3.0,        valStruct2.val3, "val3 not equal");
                    Assert.AreEqual((byte) 4,   valStruct2.val4, "val4 not equal");
                    Assert.AreEqual(5.0f,       valStruct2.val5, "val5 not equal");

                    stack.Dispose();
                })
                .WarmupCount(warmupCount)
                .MeasurementCount(measureCount)
                .Run();
        }

        private struct TestStruct1
        {
            public float val1;
            public byte val2;
            public double val3;
            public byte val4;
            public float val5;
        }
    }
}