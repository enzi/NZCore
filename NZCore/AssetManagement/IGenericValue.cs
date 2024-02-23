using System;

namespace NZSpellCasting
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