// <copyright project="NZCore" file="SIMDHelper.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using NZCore.Helper.Intrinsics;
using Unity.Burst.Intrinsics;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace NZCore
{
    public static class SIMDHelper
    {
        // public static uint ExtractMostSignificantBits<T>(this v128 vector)
        //     where T : unmanaged
        // {
        //     uint result = vector.Lo64.ExtractMostSignificantBits<T>();
        //     result |= vector.Hi64.ExtractMostSignificantBits<T>() << Vector64<T>.Count;
        //     return result;
        // }
        //
        // public static uint ExtractMostSignificantBits<T>(this v64 vector)
        //     where T : unmanaged
        // {
        //     uint result = 0;
        //
        //     for (int index = 0; index < Vector64<T>.Count; index++)
        //     {
        //         uint value = ExtractMostSignificantBit<T>(vector.GetElementUnsafe<T>(index));
        //         result |= (value << index);
        //     }
        //
        //     return result;
        // }

//         public static unsafe T GetElementUnsafe<T>(ref this v64 vector, int index)
//             where T : unmanaged
//         {
//             Debug.Assert((index >= 0) && (index < Vector64<T>.Count));
//            
//             var ptr = (byte*) UnsafeUtility.AddressOf(ref vector);
//             return *(T*) (ptr + index);
//         }
//         
//         [MethodImpl(MethodImplOptions.AggressiveInlining)]
//         public static uint ExtractMostSignificantBit<T>(T value)
//             where T : unmanaged
//         {
//             if (typeof(T) == typeof(byte))
//             {
//                 uint bits = (byte)(object)value;
//                 return bits >> 7;
//             }
//             // else if (typeof(T) == typeof(double))
//             // {
//             //     ulong bits = BitConverter.DoubleToUInt64Bits((double)(object)value);
//             //     return (uint)(bits >> 63);
//             // }
//             else if (typeof(T) == typeof(short))
//             {
//                 uint bits = (ushort)(short)(object)value;
//                 return bits >> 15;
//             }
//             else if (typeof(T) == typeof(int))
//             {
//                 uint bits = (uint)(int)(object)value;
//                 return bits >> 31;
//             }
//             else if (typeof(T) == typeof(long))
//             {
//                 ulong bits = (ulong)(long)(object)value;
//                 return (uint)(bits >> 63);
//             }
//             else if (typeof(T) == typeof(nint))
//             {
// #if TARGET_64BIT
//                 ulong bits = (ulong)(nint)(object)value;
//                 return (uint)(bits >> 63);
// #else
//                 uint bits = (uint)(nint)(object)value;
//                 return bits >> 31;
// #endif
//             }
//             else if (typeof(T) == typeof(nuint))
//             {
// #if TARGET_64BIT
//                 ulong bits = (ulong)(nuint)(object)value;
//                 return (uint)(bits >> 63);
// #else
//                 uint bits = (uint)(nuint)(object)value;
//                 return bits >> 31;
// #endif
//             }
//             else if (typeof(T) == typeof(sbyte))
//             {
//                 uint bits = (byte)(sbyte)(object)value;
//                 return bits >> 7;
//             }
//             // else if (typeof(T) == typeof(float))
//             // {
//             //     uint bits = BitConverter.single  .SingleToUInt32Bits((float)(object)value);
//             //     return bits >> 31;
//             // }
//             else if (typeof(T) == typeof(ushort))
//             {
//                 uint bits = (ushort)(object)value;
//                 return bits >> 15;
//             }
//             else if (typeof(T) == typeof(uint))
//             {
//                 uint bits = (uint)(object)value;
//                 return bits >> 31;
//             }
//             else if (typeof(T) == typeof(ulong))
//             {
//                 ulong bits = (ulong)(object)value;
//                 return (uint)(bits >> 63);
//             }
//             else
//             {
//                 throw new NotImplementedException();
//             }
//         }
    }
}