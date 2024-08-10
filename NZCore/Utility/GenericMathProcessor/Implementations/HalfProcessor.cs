// <copyright project="NZCore" file="HalfProcessor.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using Unity.Mathematics;

namespace NZCore
{
    public static partial class GenericMathProcessor
    {
        public struct HalfProcessor : IGenericValueCalculator<half>
        {
            public half Add(half a, half b)
            {
                return (half)(a + b);
            }

            public half Subtract(half a, half b)
            {
                return (half)(a - b);
            }

            public half Multiply(half a, half b)
            {
                return (half)(a * b);
            }

            public half Divide(half a, half b)
            {
                return (half)(a / b);
            }

            public half PowerAtoB(half a, half b)
            {
                return (half)math.pow(a, b);
            }

            public half PowerBtoA(half a, half b)
            {
                return (half)math.pow(b, a);
            }

            public half Min(half a, half b)
            {
                return (half)math.min(a, b);
            }

            public half Max(half a, half b)
            {
                return (half)math.max(a, b);
            }
        }
    }
}