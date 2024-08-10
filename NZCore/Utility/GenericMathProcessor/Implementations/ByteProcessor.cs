// <copyright project="NZCore" file="ByteProcessor.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using Unity.Mathematics;

namespace NZCore
{
    public static partial class GenericMathProcessor
    {
        public struct ByteProcessor : IGenericValueCalculator<byte>
        {
            public byte Add(byte a, byte b)
            {
                return (byte)(a + b);
            }

            public byte Subtract(byte a, byte b)
            {
                return (byte)(a - b);
            }

            public byte Multiply(byte a, byte b)
            {
                return (byte)(a * b);
            }

            public byte Divide(byte a, byte b)
            {
                return (byte)(a / b);
            }

            public byte PowerAtoB(byte a, byte b)
            {
                return (byte)math.pow(a, b);
            }

            public byte PowerBtoA(byte a, byte b)
            {
                return (byte)math.pow(b, a);
            }

            public byte Min(byte a, byte b)
            {
                return (byte)math.min((int)a, (int)b);
            }

            public byte Max(byte a, byte b)
            {
                return (byte)math.max((int)a, (int)b);
            }
        }
    }
}