// <copyright project="NZCore" file="InterlockedExtensions.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System.Threading;

namespace NZCore
{
    public static class InterlockedExtensions
    {
        public static double Add(ref double location1, double value)
        {
            double newCurrentValue = location1; // non-volatile read, so may be stale
            while (true)
            {
                double currentValue = newCurrentValue;
                double newValue = currentValue + value;
                newCurrentValue = Interlocked.CompareExchange(ref location1, newValue, currentValue);
                if (newCurrentValue.Equals(currentValue)) // see "Update" below
                    return newValue;
            }
        }

        public static float Add(ref float location1, float value)
        {
            float newCurrentValue = location1; // non-volatile read, so may be stale
            while (true)
            {
                float currentValue = newCurrentValue;
                float newValue = currentValue + value;
                newCurrentValue = Interlocked.CompareExchange(ref location1, newValue, currentValue);
                if (newCurrentValue.Equals(currentValue)) // see "Update" below
                    return newValue;
            }
        }

        public static int Add(ref int location1, int value)
        {
            return Interlocked.Add(ref location1, value);
        }

        public static long Add(ref long location1, long value)
        {
            return Interlocked.Add(ref location1, value);
        }
    }
}