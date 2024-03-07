using Unity.Mathematics;

namespace NZCore
{
    public static partial class GenericMathProcessor
    {
        public struct UIntProcessor : IGenericValueCalculator<uint>
        {
            public uint Add(uint a, uint b)
            {
                return a + b;
            }

            public uint Subtract(uint a, uint b)
            {
                return a - b;
            }

            public uint Multiply(uint a, uint b)
            {
                return a * b;
            }

            public uint Divide(uint a, uint b)
            {
                return a / b;
            }

            public uint PowerAtoB(uint a, uint b)
            {
                return (uint)math.pow(a, b);
            }

            public uint PowerBtoA(uint a, uint b)
            {
                return (uint)math.pow(b, a);
            }

            public uint Min(uint a, uint b)
            {
                return math.min(a, b);
            }

            public uint Max(uint a, uint b)
            {
                return math.max(a, b);
            }
        }
    }
}