using Unity.Mathematics;

namespace NZCore
{
    public static partial class GenericMathProcessor
    {
        public struct FloatProcessor : IGenericValueCalculator<float>
        {
            public float Add(float a, float b)
            {
                return a + b;
            }

            public float Subtract(float a, float b)
            {
                return a - b;
            }

            public float Multiply(float a, float b)
            {
                return a * b;
            }

            public float Divide(float a, float b)
            {
                return a / b;
            }

            public float PowerAtoB(float a, float b)
            {
                return math.pow(a, b);
            }

            public float PowerBtoA(float a, float b)
            {
                return math.pow(b, a);
            }

            public float Min(float a, float b)
            {
                return math.min(a, b);
            }

            public float Max(float a, float b)
            {
                return math.max(a, b);
            }
        }
    }
}