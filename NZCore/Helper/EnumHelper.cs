// <copyright project="NZCore" file="EnumHelper.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Unity.Burst;

namespace NZSpellCasting
{
    [BurstCompile]
    public static class EnumHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this byte keys, byte flag)
        {
            return (keys & flag) == flag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsAny(this byte keys, byte flag)
        {
            return (keys & flag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this int keys, int flag)
        {
            return (keys & flag) == flag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsAny(this int keys, int flag)
        {
            return (keys & flag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this uint keys, uint flag)
        {
            return (keys & flag) == flag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsAny(this uint keys, uint flag)
        {
            return (keys & flag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this ulong keys, ulong flag)
        {
            return (keys & flag) == flag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsAny(this ulong keys, ulong flag)
        {
            return (keys & flag) != 0uL;
        }
    }
}