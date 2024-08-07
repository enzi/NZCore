﻿using Unity.Burst.Intrinsics;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore.Helper.Intrinsics
{
    public readonly unsafe struct Vector64<T>
        where T : unmanaged
    {
        public const int Size = 8;
        public const int Alignment = 8;

        public static int Count => Size / UnsafeUtility.SizeOf<T>();
    }
}