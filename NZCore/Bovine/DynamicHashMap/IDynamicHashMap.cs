// <copyright project="NZCore" file="IDynamicHashMap.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.Core.Iterators
{
    public interface IDynamicHashMapDebugProxy { }
    public interface IDynamicHashMap<TKey, TValue> : IDynamicHashMapDebugProxy, IDynamicHashMapBase<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
    }
}
