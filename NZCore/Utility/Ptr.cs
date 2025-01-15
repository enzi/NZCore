// <copyright project="NZCore" file="Ptr.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    public readonly unsafe struct Ptr<T> : IEquatable<Ptr<T>>
        where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        public readonly T* Value;
        
        public bool IsCreated => Value != null;
        public ref T Ref => ref UnsafeUtility.AsRef<T>(Value);
        
        public Ptr(T* value)
        {
            Value = value;
        }
        
        public Ptr(ref T value)
        {
            Value = (T*)UnsafeUtility.AddressOf(ref value);
        }

        public static implicit operator T*(Ptr<T> ptrStruct)
        {
            return ptrStruct.Value;
        }
        
        public static implicit operator Ptr<T>(T* ptr)
        {
            return new Ptr<T>(ptr);
        }

        public static bool operator ==(Ptr<T> left, Ptr<T> right)
        {
            return left.Equals(right);
        }
        
        public static bool operator !=(Ptr<T> left, Ptr<T> right)
        {
            return !left.Equals(right);
        }
        
        public bool Equals(Ptr<T> other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is Ptr<T> other && this.Equals(other);
        }
        
        public override int GetHashCode()
        {
            return unchecked((int)(long) Value);
        }
    }
}