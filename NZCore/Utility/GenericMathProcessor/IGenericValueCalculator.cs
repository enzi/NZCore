// <copyright project="NZCore" file="IGenericValueCalculator.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

namespace NZCore
{
    public interface IGenericValueCalculator<T>
    {
        T Add(T a, T b);
        T Subtract(T a, T b);
        T Multiply(T a, T b);
        T Divide(T a, T b);
        T PowerAtoB(T a, T b);
        T PowerBtoA(T a, T b);
        T Min(T a, T b);
        T Max(T a, T b);
    }
}