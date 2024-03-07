using System;

namespace NZCore
{
    public interface IGenericValue<T> : IEquatable<T>
    {
        public T Value
        {
            get;
            set;
        }

        //bool Equals(int other);
    }
}