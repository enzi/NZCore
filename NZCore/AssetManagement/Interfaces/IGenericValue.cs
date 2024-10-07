// <copyright project="NZCore" file="IGenericValue.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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