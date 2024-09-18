// <copyright project="NZCore" file="GenericUnionValue.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace NZCore
{
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct GenericUnionValue
    {
        // 64 bit
        [FieldOffset(0)] public double DoubleValue;
        [FieldOffset(0)] public double LongValue;
        [FieldOffset(0)] public double ULongValue;

        // 32 bit
        [FieldOffset(0)] public float FloatValue;
        [FieldOffset(0)] public int IntValue;

        [FieldOffset(0)] public uint UIntValue;

        // 16 bit
        [FieldOffset(0)] public short ShortValue;

        [FieldOffset(0)] public half HalfValue;

        // 8 bit
        [FieldOffset(0)] public byte ByteValue;
        [FieldOffset(0)] public bool BoolValue;
    }
}