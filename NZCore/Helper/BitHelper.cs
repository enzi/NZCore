using System.Runtime.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace NZCore.Helper
{
    public static unsafe class BitHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSet(ulong* ptr, int pos)
        {
            var idx = pos >> 6;
            var shift = pos & 0x3f;
            var mask = 1ul << shift;
            return 0ul != (ptr[idx] & mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Set(ulong* ptr, int pos, bool value)
        {
            var idx = pos >> 6;
            var shift = pos & 0x3f;
            var mask = 1ul << shift;
            var bits = (ptr[idx] & ~mask) | ((ulong)-Bitwise.FromBool(value) & mask);
            ptr[idx] = bits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSet(in v128 bitField, int pos)
        {
            var idx = pos >> 6;
            var shift = pos & 0x3f;
            var mask = 1ul << shift;
            return 0ul != (idx == 0 ? bitField.ULong0 & mask : bitField.ULong1 & mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Set(ref v128 bitField, int pos, bool value)
        {
            var idx = pos >> 6;
            var shift = pos & 0x3f;
            var mask = 1ul << shift;

            if (idx == 0)
            {
                var bits = bitField.ULong0 & ~mask | ((ulong)-Bitwise.FromBool(value) & mask);
                bitField.ULong0 = bits;
            }
            else
            {
                var bits = bitField.ULong1 & ~mask | ((ulong)-Bitwise.FromBool(value) & mask);
                bitField.ULong1 = bits;
            }
        }

        public static uint RoundToPowerOf2(uint value)
        {
            Debug.Assert(value > 0 && value <= (uint.MaxValue / 2) + 1);

            return 1u << (32 - math.lzcnt(value - 1));
        }
    }
}