﻿// <copyright project="NZCore" file="BTreeEntry.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.NativeContainers.BTree
{
    public struct BTreeEntry<TKey, TValue> : IEquatable<BTreeEntry<TKey, TValue>>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        public TKey Key;
        public TValue Pointer;

        public bool Equals(BTreeEntry<TKey, TValue> other)
        {
            return Key.Equals(other.Key) && Pointer.Equals(other.Pointer);
        }
    }
}