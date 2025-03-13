// <copyright project="NZCore" file="GenericDataType.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Assertions;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace NZCore
{
    [Serializable]
    public enum GenericDataType : byte
    {
        None,

        // 16 bits
        Half,
        Short,
        UShort,

        // 32 bits
        Float,
        Int,
        UInt,

        // 64 bits
        Double,
        Long,
        ULong,

        // 8 bits
        Byte,
        Bool
    }

    public static class GenericDataTypeExtensions
    {
        public static void AddGenericValue<T>(this ref DynamicBuffer<T> buffer, GenericDataType dataType, double value)
            where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var typeSize = UnsafeUtility.SizeOf<T>();
            Assert.AreEqual(typeSize, 1, $"AddStat -> {nameof(T)} needs to be size 1!");
#endif
            
            switch (dataType)
            {
                case GenericDataType.Bool:
                case GenericDataType.Byte:
                {
                    buffer.AddToByteBuffer((byte)value);
                    break;
                }
                case GenericDataType.Short:
                {
                    buffer.AddToByteBuffer((short)value);
                    break;
                }
                case GenericDataType.UShort:
                {
                    buffer.AddToByteBuffer((ushort)value);
                    break;
                }
                case GenericDataType.Half:
                {
                    buffer.AddToByteBuffer((half)value);
                    break;
                }
                case GenericDataType.Float:
                {
                    buffer.AddToByteBuffer((float)value);
                    //Debug.Log($"set {statType} to {value}");
                    break;
                }
                case GenericDataType.Int:
                {
                    buffer.AddToByteBuffer((int)value);
                    break;
                }
                case GenericDataType.UInt:
                {
                    buffer.AddToByteBuffer((uint)value);
                    break;
                }
                case GenericDataType.Double:
                {
                    buffer.AddToByteBuffer((double)value);
                    //Debug.Log($"set {statType} to {value}");
                    break;
                }
                case GenericDataType.Long:
                {
                    buffer.AddToByteBuffer((long)value);
                    break;
                }
                case GenericDataType.ULong:
                {
                    buffer.AddToByteBuffer((ulong)value);
                    break;
                }
                case GenericDataType.None:
                    //Debug.Log($"{statType} is none!");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
            }
        }
    }
}