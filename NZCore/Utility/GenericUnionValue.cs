// <copyright project="NZCore" file="GenericUnionValue.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace NZCore
{
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct GenericUnionValue
    {
        // 64 bit
        [FieldOffset(0)] public double DoubleValue;
        [FieldOffset(0)] public long LongValue;
        [FieldOffset(0)] public ulong ULongValue;

        // 32 bit
        [FieldOffset(0)] public float FloatValue;
        [FieldOffset(0)] public int IntValue;
        [FieldOffset(0)] public uint UIntValue;

        // 16 bit
        [FieldOffset(0)] public half HalfValue;
        [FieldOffset(0)] public short ShortValue;
        [FieldOffset(0)] public ushort UShortValue;

        // 8 bit
        [FieldOffset(0)] public byte ByteValue;
        [FieldOffset(0)] public bool BoolValue;
        
        public static unsafe GenericUnionValue Create(StatDataType dataType, byte* valuePtr)
        {
            switch (dataType)
            {
                case StatDataType.Short:
                    return new GenericUnionValue() { ShortValue = *(short*)valuePtr };
                case StatDataType.UShort:
                    return new GenericUnionValue() { UShortValue = *(ushort*)valuePtr };
                case StatDataType.Half:
                    return new GenericUnionValue() { HalfValue = *(half*)valuePtr };
                case StatDataType.Float:
                    return new GenericUnionValue() { FloatValue = *(float*)valuePtr };
                case StatDataType.Int:
                    return new GenericUnionValue() { IntValue = *(int*)valuePtr };
                case StatDataType.UInt:
                    return new GenericUnionValue() { UIntValue = *(uint*)valuePtr };
                case StatDataType.Double:
                    return new GenericUnionValue() { DoubleValue = *(double*)valuePtr };
                case StatDataType.Long:
                    return new GenericUnionValue() { LongValue = *(long*)valuePtr };
                case StatDataType.ULong:
                    return new GenericUnionValue() { ULongValue = *(ulong*)valuePtr };
                case StatDataType.Byte:
                    return new GenericUnionValue() { ByteValue = *(byte*)valuePtr };
                case StatDataType.Bool:
                    return new GenericUnionValue() { BoolValue = *(byte*)valuePtr > 0 };
                case StatDataType.None:
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
            }
        }
        
        public static GenericUnionValue Create(StatDataType dataType, double value)
        {
            switch (dataType)
            {
                case StatDataType.Short:
                    return new GenericUnionValue() { ShortValue = (short)value };
                case StatDataType.UShort:
                    return new GenericUnionValue() { UShortValue = (ushort)value };
                case StatDataType.Half:
                    return new GenericUnionValue() { HalfValue = (half)value };
                case StatDataType.Float:
                    return new GenericUnionValue() { FloatValue = (float)value };
                case StatDataType.Int:
                    return new GenericUnionValue() { IntValue = (int)value };
                case StatDataType.UInt:
                    return new GenericUnionValue() { UIntValue = (uint)value };
                case StatDataType.Double:
                    return new GenericUnionValue() { DoubleValue = (double)value };
                case StatDataType.Long:
                    return new GenericUnionValue() { LongValue = (long)value };
                case StatDataType.ULong:
                    return new GenericUnionValue() { ULongValue = (ulong)value };
                case StatDataType.Byte:
                    return new GenericUnionValue() { ByteValue = (byte)value };
                case StatDataType.Bool:
                    return new GenericUnionValue() { BoolValue = value > 0 };
                case StatDataType.None:
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
            }
        }
        
        public static GenericUnionValue Create(TriggerDataType dataType, double value)
        {
            switch (dataType)
            {
                case TriggerDataType.Short:
                    return new GenericUnionValue() { ShortValue = (short)value };
                case TriggerDataType.UShort:
                    return new GenericUnionValue() { UShortValue = (ushort)value };
                case TriggerDataType.Half:
                    return new GenericUnionValue() { HalfValue = (half)value };
                case TriggerDataType.Float:
                    return new GenericUnionValue() { FloatValue = (float)value };
                case TriggerDataType.Int:
                    return new GenericUnionValue() { IntValue = (int)value };
                case TriggerDataType.UInt:
                    return new GenericUnionValue() { UIntValue = (uint)value };
                case TriggerDataType.Double:
                    return new GenericUnionValue() { DoubleValue = (double)value };
                case TriggerDataType.Long:
                    return new GenericUnionValue() { LongValue = (long)value };
                case TriggerDataType.ULong:
                    return new GenericUnionValue() { ULongValue = (ulong)value };
                case TriggerDataType.Byte:
                    return new GenericUnionValue() { ByteValue = (byte)value };
                case TriggerDataType.Bool:
                    return new GenericUnionValue() { BoolValue = value > 0 };
                case TriggerDataType.None:
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
            }
        }
    }

    public static class GenericUnionValueExtensions
    {
    }
}