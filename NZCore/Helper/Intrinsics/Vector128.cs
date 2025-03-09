// <copyright project="NZCore" file="Vector128.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections.LowLevel.Unsafe;

namespace NZCore.Helper.Intrinsics
{
    public struct Vector128<T>
        where T : unmanaged
    {
        public const int Size = 16;
        public const int Alignment = 16;

        public static int Count => Size / UnsafeUtility.SizeOf<T>();
    }
}