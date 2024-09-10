// <copyright project="NZCore" file="DoubleProcessor.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Mathematics;

namespace NZCore
{
    public static partial class GenericMathProcessor
    {
        public struct DoubleProcessor : IGenericValueCalculator<double>
        {
            public double Add(double a, double b)
            {
                return a + b;
            }

            public double Subtract(double a, double b)
            {
                return a - b;
            }

            public double Multiply(double a, double b)
            {
                return a * b;
            }

            public double Divide(double a, double b)
            {
                return a / b;
            }

            public double PowerAtoB(double a, double b)
            {
                return (double)math.pow(a, b);
            }

            public double PowerBtoA(double a, double b)
            {
                return (double)math.pow(b, a);
            }

            public double Min(double a, double b)
            {
                return math.min(a, b);
            }

            public double Max(double a, double b)
            {
                return math.max(a, b);
            }
        }
    }
}