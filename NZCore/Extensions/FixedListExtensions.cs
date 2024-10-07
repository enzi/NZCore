// <copyright project="NZCore" file="FixedListExtensions.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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