// <copyright project="NZCore.MVVM" file="ModelVersion.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using UnityEngine;

namespace NZCore.MVVM
{
    /// <summary>
    /// Represents a version for model changes and migrations.
    /// </summary>
    [Serializable]
    public struct ModelVersion : IComparable<ModelVersion>, IEquatable<ModelVersion>
    {
        [SerializeField] private int _major;
        [SerializeField] private int _minor;
        [SerializeField] private int _patch;

        /// <summary>
        /// Gets the major version number.
        /// </summary>
        public int Major => _major;

        /// <summary>
        /// Gets the minor version number.
        /// </summary>
        public int Minor => _minor;

        /// <summary>
        /// Gets the patch version number.
        /// </summary>
        public int Patch => _patch;

        /// <summary>
        /// Initializes a new instance of the ModelVersion struct.
        /// </summary>
        /// <param name="major">The major version.</param>
        /// <param name="minor">The minor version.</param>
        /// <param name="patch">The patch version.</param>
        public ModelVersion(int major, int minor = 0, int patch = 0)
        {
            _major = Math.Max(0, major);
            _minor = Math.Max(0, minor);
            _patch = Math.Max(0, patch);
        }

        /// <summary>
        /// Gets the default version (0.0.0).
        /// </summary>
        public static ModelVersion Default => new ModelVersion(0, 0, 0);

        /// <summary>
        /// Compares this version with another version.
        /// </summary>
        /// <param name="other">The other version to compare.</param>
        /// <returns>A value indicating the relative order.</returns>
        public int CompareTo(ModelVersion other)
        {
            var majorComparison = _major.CompareTo(other._major);
            if (majorComparison != 0) return majorComparison;

            var minorComparison = _minor.CompareTo(other._minor);
            return minorComparison != 0 ? minorComparison : _patch.CompareTo(other._patch);
        }

        /// <summary>
        /// Determines whether this version is equal to another version.
        /// </summary>
        /// <param name="other">The other version to compare.</param>
        /// <returns>True if the versions are equal; otherwise, false.</returns>
        public bool Equals(ModelVersion other)
        {
            return _major == other._major && _minor == other._minor && _patch == other._patch;
        }

        /// <summary>
        /// Determines whether this version is equal to another object.
        /// </summary>
        /// <param name="obj">The other object to compare.</param>
        /// <returns>True if the objects are equal; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return obj is ModelVersion other && Equals(other);
        }

        /// <summary>
        /// Gets the hash code for this version.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = _major;
                hashCode = (hashCode * 397) ^ _minor;
                hashCode = (hashCode * 397) ^ _patch;
                return hashCode;
            }
        }

        /// <summary>
        /// Returns the string representation of this version.
        /// </summary>
        /// <returns>The version string in format "major.minor.patch".</returns>
        public override string ToString()
        {
            return $"{_major}.{_minor}.{_patch}";
        }

        /// <summary>
        /// Determines whether two versions are equal.
        /// </summary>
        public static bool operator ==(ModelVersion left, ModelVersion right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two versions are not equal.
        /// </summary>
        public static bool operator !=(ModelVersion left, ModelVersion right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Determines whether the left version is less than the right version.
        /// </summary>
        public static bool operator <(ModelVersion left, ModelVersion right)
        {
            return left.CompareTo(right) < 0;
        }

        /// <summary>
        /// Determines whether the left version is greater than the right version.
        /// </summary>
        public static bool operator >(ModelVersion left, ModelVersion right)
        {
            return left.CompareTo(right) > 0;
        }

        /// <summary>
        /// Determines whether the left version is less than or equal to the right version.
        /// </summary>
        public static bool operator <=(ModelVersion left, ModelVersion right)
        {
            return left.CompareTo(right) <= 0;
        }

        /// <summary>
        /// Determines whether the left version is greater than or equal to the right version.
        /// </summary>
        public static bool operator >=(ModelVersion left, ModelVersion right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}