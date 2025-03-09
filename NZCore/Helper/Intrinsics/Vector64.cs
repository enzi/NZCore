// <copyright project="NZCore" file="Vector64.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections.LowLevel.Unsafe;

namespace NZCore.Helper.Intrinsics
{
    public readonly struct Vector64<T>
        where T : unmanaged
    {
        public const int Size = 8;
        public const int Alignment = 8;

        public static int Count => Size / UnsafeUtility.SizeOf<T>();
    }
}