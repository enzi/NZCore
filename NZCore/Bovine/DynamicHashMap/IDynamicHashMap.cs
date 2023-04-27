// <copyright file="IDynamicHashMap.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace NZCore.Core.Iterators
{
    using System;

    public interface IDynamicHashMapDebugProxy { }
    public interface IDynamicHashMap<TKey, TValue> : IDynamicHashMapDebugProxy, IDynamicHashMapBase<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
    }
}
