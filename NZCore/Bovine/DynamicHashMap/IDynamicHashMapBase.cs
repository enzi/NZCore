﻿// <copyright project="NZCore" file="IDynamicHashMapBase.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Entities;

namespace NZCore.Core.Iterators
{
    [SuppressMessage("ReSharper", "UnusedTypeParameter", Justification = "Needed for safety.")]
    [SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Defines memory layout")]
    public interface IDynamicHashMapBase<TKey, TValue> : IBufferElementData
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        byte Value { get; }
    }
}
