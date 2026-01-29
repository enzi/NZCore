// <copyright project="NZCore" file="TagMask128.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Burst;
using Unity.Mathematics;
using Unity.Properties;

namespace NZCore
{
    [BurstCompile]
    public struct TagMask128
    {
        private uint4 _bits;

        [CreateProperty] public uint4 Bits => _bits;

        public bool IsEmpty => !math.any(_bits);

        public void Set(int tagIndex)
        {
            int chunk = tagIndex >> 5; // divide by 32
            int bit = tagIndex & 31; // mod 32
            _bits[chunk] |= 1u << bit;
        }

        public readonly bool Has(int tagIndex)
        {
            int chunk = tagIndex >> 5;
            int bit = tagIndex & 31;
            return (_bits[chunk] & (1u << bit)) != 0;
        }

        public readonly bool HasAny(in TagMask128 query)
        {
            return math.any((_bits & query._bits) != 0);
        }

        public readonly bool HasAll(in TagMask128 query)
        {
            return math.all((_bits & query._bits) == query._bits);
        }
    }
}