// <copyright project="NZCore" file="IGenericValueCalculator.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore
{
    public interface IGenericValueCalculator<T>
    {
        T Add(T a, T b) { return default; }
        T Subtract(T a, T b) { return default; }
        T Multiply(T a, T b) { return default; }
        T Divide(T a, T b) { return default; }
        T PowerAtoB(T a, T b) { return default; }
        T PowerBtoA(T a, T b) { return default; }
        T Min(T a, T b) { return default; }
        T Max(T a, T b) { return default; }
        
        // Math functions

        T Abs(T a) { return default; }
        T Ceil(T a) { return default; }
        T Floor(T a) { return default; }
        T Round(T a) { return default; }
        T Log10(T a) { return default; }
        T NaturalLog(T a) { return default; }

        // Logic
        bool Any(T a, T b) { return default; }
        bool Equal(T a, T b) { return default; }
        bool NotEqual(T a, T b) { return default; }
        bool GreaterThan(T a, T b) { return default; }
        bool LesserThan(T a, T b) { return default; }
        bool GreaterEqual(T a, T b) { return default; }
        bool LesserEqual(T a, T b) { return default; }
        bool HasFlag(T a, T b) { return default; }
    }
}