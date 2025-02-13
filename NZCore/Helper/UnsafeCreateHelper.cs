// <copyright project="NZCore" file="UnsafeCreateHelper.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    public static unsafe class UnsafeCreateHelper
    {
        public static UnsafeHashMap<TKey, TValue>* CreateHashMap<TKey, TValue>(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            UnsafeHashMap<TKey, TValue>* hashMap = AllocatorManager.Allocate<UnsafeHashMap<TKey, TValue>>(allocator);
            *hashMap = new UnsafeHashMap<TKey, TValue>(initialCapacity, allocator);
            return hashMap;
        }
    }
}