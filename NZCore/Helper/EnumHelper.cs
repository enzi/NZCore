// <copyright project="NZCore" file="EnumHelper.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Unity.Burst;

namespace NZCore
{
    [BurstCompile]
    public static class EnumHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this byte keys, byte flag) => (keys & flag) == flag;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsAny(this byte keys, byte flag) => (keys & flag) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this int keys, int flag) => (keys & flag) == flag;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsAny(this int keys, int flag) => (keys & flag) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this uint keys, uint flag) => (keys & flag) == flag;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsAny(this uint keys, uint flag) => (keys & flag) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this ulong keys, ulong flag) => (keys & flag) == flag;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsAny(this ulong keys, ulong flag) => (keys & flag) != 0uL;
    }
}