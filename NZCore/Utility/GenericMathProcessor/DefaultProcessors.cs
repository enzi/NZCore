// <copyright project="NZCore" file="DefaultProcessors.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Mathematics;

namespace NZCore
{
    public static partial class GenericMathProcessor
    {
        public partial struct ByteProcessor : IGenericValueCalculator<byte>
        {
        }

        public partial struct DoubleProcessor : IGenericValueCalculator<double>
        {
        }

        public partial struct FloatProcessor : IGenericValueCalculator<float>
        {
        }

        public partial struct HalfProcessor : IGenericValueCalculator<half>
        {
        }

        public partial struct IntProcessor : IGenericValueCalculator<int>
        {
        }

        //public partial struct NZValueProcessor : IGenericValueCalculator<NZValue> { }
        public partial struct ShortProcessor : IGenericValueCalculator<short>
        {
        }

        public partial struct UIntProcessor : IGenericValueCalculator<uint>
        {
        }

        public partial struct LongProcessor : IGenericValueCalculator<long>
        {
        }

        public partial struct ULongProcessor : IGenericValueCalculator<ulong>
        {
        }

        // public partial struct BoolProcessor : IGenericValueCalculator<bool>
        // {
        // }
    }
}