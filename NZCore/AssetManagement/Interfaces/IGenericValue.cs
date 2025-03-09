// <copyright project="NZCore" file="IGenericValue.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore
{
    public interface IGenericValue<T> : IEquatable<T>
        where T : unmanaged
    {
        public T Value { get; set; }
    }
}