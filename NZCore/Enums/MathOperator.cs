// <copyright project="NZCore" file="MathOperator.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore
{
    public enum MathOperator : byte
    {
        Set,
        Add,
        Subtract,
        Multiply,
        Divide,
        PowerAtoB,
        PowerBtoA,
        Min,
        Max
    }

    public enum MathFunction : byte
    {
        Abs,
        Ceil,
        Floor,
        Round,
        Log10,
        NaturalLog,
    }
}