// <copyright project="NZCore" file="InterlockedExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Threading;

namespace NZCore
{
    public static class InterlockedExtensions
    {
        public static double Add(ref double location1, double value)
        {
            var newCurrentValue = location1; // non-volatile read, so may be stale
            while (true)
            {
                var currentValue = newCurrentValue;
                var newValue = currentValue + value;
                newCurrentValue = Interlocked.CompareExchange(ref location1, newValue, currentValue);
                if (newCurrentValue.Equals(currentValue)) // see "Update" below
                {
                    return newValue;
                }
            }
        }

        public static float Add(ref float location1, float value)
        {
            var newCurrentValue = location1; // non-volatile read, so may be stale
            while (true)
            {
                var currentValue = newCurrentValue;
                var newValue = currentValue + value;
                newCurrentValue = Interlocked.CompareExchange(ref location1, newValue, currentValue);
                if (newCurrentValue.Equals(currentValue)) // see "Update" below
                {
                    return newValue;
                }
            }
        }

        public static int Add(ref int location1, int value) => Interlocked.Add(ref location1, value);

        public static long Add(ref long location1, long value) => Interlocked.Add(ref location1, value);
    }
}