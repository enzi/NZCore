using System;

namespace NZCore
{
    public interface IGenericValue<T> : IEquatable<T>
        where T : unmanaged
    {
        public T Value { get; set; }
    }
}