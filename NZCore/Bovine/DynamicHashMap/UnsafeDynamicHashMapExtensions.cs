﻿// <copyright project="NZCore" file="UnsafeDynamicHashMapExtensions.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.Core.Iterators
{
    public static unsafe class UnsafeDynamicHashMapExtensions
    {
        public static bool FindFirst<TKey, TValue>(this DynamicMultiHashMap<TKey, TValue> hashMap, TKey key, TValue value, out TValue* result)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged, IEquatable<TValue>
        {
            return DynamicHashMapBase<TKey, TValue>.FindFirst(hashMap.BufferReadOnly, key, value, out result);
        }

        public static bool Contains<TKey, TValue>(this DynamicMultiHashMap<TKey, TValue> hashMap, TKey key, TValue value)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged, IEquatable<TValue>
        {
            return DynamicHashMapBase<TKey, TValue>.FindFirst(hashMap.BufferReadOnly, key, value, out _);
        }
    }
}
