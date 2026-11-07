// <copyright project="NZCore" file="PrefabId.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Collections;
using Unity.Entities;

namespace NZCore.AssetManagement
{
    [Serializable]
    public struct PrefabId : IComponentData, IEquatable<PrefabId>
    {
        public const int MaxGroup = 255;
        public const int MaxId = (1 << IdBits) - 1;

        private const int IdBits = 24;

        public static readonly PrefabId Null = default;

        public int Value;

        public PrefabId(int group, int id)
        {
            if (group < 1 || group > MaxGroup)
            {
                throw new ArgumentOutOfRangeException(nameof(group), "PrefabId group must be between 1 and 255");
            }

            if (id < 1 || id > MaxId)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "PrefabId ID must be between 1 and 16777215");
            }

            Value = (group << IdBits) | id;
        }

        public int Group => (int)((uint)Value >> IdBits);
        public int Id => Value & MaxId;
        public bool IsNull => Id == 0;

        public FixedString32Bytes ToFixedString()
        {
            var fs = (FixedString32Bytes)"Group:";
            fs.Append(Group);
            fs.Append((FixedString32Bytes)" ID:");
            fs.Append(Id);
            return fs;
        }

        public bool Equals(PrefabId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PrefabId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => ToFixedString().ToString();

        public static bool operator ==(PrefabId a, PrefabId b) => a.Value == b.Value;
        public static bool operator !=(PrefabId a, PrefabId b) => a.Value != b.Value;
    }
}