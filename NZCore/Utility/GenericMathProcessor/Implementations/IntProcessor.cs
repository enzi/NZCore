using Unity.Mathematics;

namespace NZCore
{
    public static partial class GenericMathProcessor
    {
        public struct IntProcessor : IGenericValueCalculator<int>
        {
            public int Add(int a, int b)
            {
                return a + b;
            }

            public int Subtract(int a, int b)
            {
                return a - b;
            }

            public int Multiply(int a, int b)
            {
                return a * b;
            }

            public int Divide(int a, int b)
            {
                return a / b;
            }

            public int PowerAtoB(int a, int b)
            {
                return (int)math.pow(a, b);
            }

            public int PowerBtoA(int a, int b)
            {
                return (int)math.pow(b, a);
            }

            public int Min(int a, int b)
            {
                return math.min(a, b);
            }

            public int Max(int a, int b)
            {
                return math.max(a, b);
            }
        }
    }
}