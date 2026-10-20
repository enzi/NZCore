// <copyright project="Unity.Entities" file="EnabledBitUtility.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Unity.Burst.Intrinsics;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    public static class EnabledBitUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetNextRange(v128 mask, int firstIndexToCheck, out int nextRangeBegin, out int nextRangeEnd) =>
            UnityEntities.EnabledBitUtility.TryGetNextRange(mask, firstIndexToCheck, out nextRangeBegin, out nextRangeEnd);
    }
}