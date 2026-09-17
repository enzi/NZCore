// <copyright project="NZCore" file="FixedListExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.Internal;
using Unity.Collections;

namespace NZCore
{
    public static unsafe class FixedListExtensions
    {
        public static T* GetPtr<T>(this FixedList4096Bytes<T> fixedList)
            where T : unmanaged =>
            fixedList.GetBuffer();
    }
}
