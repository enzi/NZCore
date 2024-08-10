// <copyright project="NZCore" file="IGenericValue.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
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