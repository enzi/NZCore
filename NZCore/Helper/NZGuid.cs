// <copyright project="NZCore" file="NZGuid.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore
{
    /// <summary>
    /// Temporary fix for Unity 6.7 alpha
    /// Blittable 16-byte replacement for System.Guid in component data.
    /// System.Guid fields make the ECS stable type hash runtime-dependent
    /// (mscorlib vs System.Private.CoreLib), which breaks entity scene loading
    /// in CoreCLR players.
    /// </summary>
    [Serializable]
    public struct NZGuid : IEquatable<NZGuid>
    {
        public ulong Value0;
        public ulong Value1;

        public static readonly NZGuid Empty = default;

        public bool IsEmpty => Value0 == 0 && Value1 == 0;

        public unsafe NZGuid(Guid guid)
        {
            this = *(NZGuid*)&guid;
        }

        public unsafe Guid ToGuid()
        {
            var copy = this;
            return *(Guid*)&copy;
        }

        public static implicit operator NZGuid(Guid guid) => new(guid);
        public static implicit operator Guid(NZGuid guid) => guid.ToGuid();

        public bool Equals(NZGuid other) => Value0 == other.Value0 && Value1 == other.Value1;
        public override bool Equals(object obj) => obj is NZGuid other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Value0, Value1);
        public override string ToString() => ToGuid().ToString();

        public static bool operator ==(NZGuid a, NZGuid b) => a.Equals(b);
        public static bool operator !=(NZGuid a, NZGuid b) => !a.Equals(b);
    }
}
