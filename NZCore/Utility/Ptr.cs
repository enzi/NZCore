// <copyright project="NZCore" file="Ptr.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    public readonly unsafe struct Ptr<T> : IEquatable<Ptr<T>>
        where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] public readonly T* Value;

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

        public static implicit operator T*(Ptr<T> ptrStruct) => ptrStruct.Value;

        public static implicit operator Ptr<T>(T* ptr) => new(ptr);

        public static bool operator ==(Ptr<T> left, Ptr<T> right) => left.Equals(right);

        public static bool operator !=(Ptr<T> left, Ptr<T> right) => !left.Equals(right);

        public bool Equals(Ptr<T> other) => Value == other.Value;

        public override bool Equals(object obj) => obj is Ptr<T> other && Equals(other);

        public override int GetHashCode() => unchecked((int)(long)Value);
    }
}