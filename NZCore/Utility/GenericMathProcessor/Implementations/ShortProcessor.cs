// <copyright project="NZCore" file="ShortProcessor.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Mathematics;

namespace NZCore
{
    public static partial class GenericMathProcessor
    {
        public struct ShortProcessor : IGenericValueCalculator<short>
        {
            public short Add(short a, short b)
            {
                return (short)(a + b);
            }

            public short Subtract(short a, short b)
            {
                return (short)(a - b);
            }

            public short Multiply(short a, short b)
            {
                return (short)(a * b);
            }

            public short Divide(short a, short b)
            {
                return (short)(a / b);
            }

            public short PowerAtoB(short a, short b)
            {
                return (short)math.pow(a, b);
            }

            public short PowerBtoA(short a, short b)
            {
                return (short)math.pow(b, a);
            }

            public short Min(short a, short b)
            {
                return (short)math.min(a, b);
            }

            public short Max(short a, short b)
            {
                return (short)math.max(a, b);
            }
        }
    }
}