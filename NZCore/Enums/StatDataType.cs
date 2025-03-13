// <copyright project="NZCore" file="StatDataType.cs">
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
    public enum StatDataType : byte
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

    public static class StatDataTypeExtensions
    {
        public static GenericDataType ToGenericDataType(this StatDataType statDataType)
        {
            return (GenericDataType)statDataType;
        }
        
        public static void AddStat<T>(this ref DynamicBuffer<T> buffer, StatDataType dataType, double value)
            where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var typeSize = UnsafeUtility.SizeOf<T>();
            Assert.AreEqual(typeSize, 1, $"AddStat -> {nameof(T)} needs to be size 1!");
#endif
            
            switch (dataType)
            {
                case StatDataType.Bool:
                case StatDataType.Byte:
                {
                    buffer.AddToByteBuffer((byte)value);
                    break;
                }
                case StatDataType.Short:
                {
                    buffer.AddToByteBuffer((short)value);
                    break;
                }
                case StatDataType.UShort:
                {
                    buffer.AddToByteBuffer((ushort)value);
                    break;
                }
                case StatDataType.Half:
                {
                    buffer.AddToByteBuffer((half)value);
                    break;
                }
                case StatDataType.Float:
                {
                    buffer.AddToByteBuffer((float)value);
                    //Debug.Log($"set {statType} to {value}");
                    break;
                }
                case StatDataType.Int:
                {
                    buffer.AddToByteBuffer((int)value);
                    break;
                }
                case StatDataType.UInt:
                {
                    buffer.AddToByteBuffer((uint)value);
                    break;
                }
                case StatDataType.Double:
                {
                    buffer.AddToByteBuffer((double)value);
                    //Debug.Log($"set {statType} to {value}");
                    break;
                }
                case StatDataType.Long:
                {
                    buffer.AddToByteBuffer((long)value);
                    break;
                }
                case StatDataType.ULong:
                {
                    buffer.AddToByteBuffer((ulong)value);
                    break;
                }
                case StatDataType.None:
                    //Debug.Log($"{statType} is none!");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
            }
        }
    }
}