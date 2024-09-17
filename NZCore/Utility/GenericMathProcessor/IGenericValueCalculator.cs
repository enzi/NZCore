// <copyright project="NZCore" file="IGenericValueCalculator.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore
{
    public interface IGenericValueCalculator<T>
    {
        T Add(T a, T b)
        {
            return default;
        }

        T Subtract(T a, T b)
        {
            return default;
        }

        T Multiply(T a, T b)
        {
            return default;
        }

        T Divide(T a, T b)
        {
            return default;
        }

        T PowerAtoB(T a, T b)
        {
            return default;
        }

        T PowerBtoA(T a, T b)
        {
            return default;
        }

        T Min(T a, T b)
        {
            return default;
        }

        T Max(T a, T b)
        {
            return default;
        }
    }
}