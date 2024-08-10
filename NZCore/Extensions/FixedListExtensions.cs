// <copyright project="NZCore" file="FixedListExtensions.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
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