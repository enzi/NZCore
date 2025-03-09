// <copyright project="NZCore" file="FixedListExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections;

namespace NZCore
{
    public static unsafe class FixedListExtensions
    {
        public static T* GetPtr<T>(this FixedList4096Bytes<T> fixedList)
            where T : unmanaged
        {
            return (T*)fixedList.Buffer;
        }
    }
}