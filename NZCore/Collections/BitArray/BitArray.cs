// <copyright project="NZCore" file="BitArray.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

// Original implementation from UnityEngine.Rendering
namespace NZCore
{
    /// <summary>
    /// IBitArray interface.
    /// </summary>
    public interface IBitArray<T> : IEquatable<T>
        where T : unmanaged, IBitArray<T>
    {
        /// <summary>Gets the capacity of this BitArray. This is the number of bits that are usable.</summary>
        uint Capacity { get; }

        /// <summary>Return `true` if all the bits of this BitArray are set to 0. Returns `false` otherwise.</summary>
        bool AllFalse { get; }

        /// <summary>Return `true` if all the bits of this BitArray are set to 1. Returns `false` otherwise.</summary>
        bool AllTrue { get; }

        /// <summary>
        /// An indexer that allows access to the bit at a given index. This provides both read and write access.
        /// </summary>
        /// <param name="index">Index of the bit.</param>
        /// <value>State of the bit at the provided index.</value>
        bool this[uint index] { get; set; }

        /// <summary>Writes the bits in the array in a human-readable form. This is as a string of 0s and 1s packed by 8 bits. This is useful for debugging.</summary>
        string HumanizedData { get; }

        /// <summary>
        /// Perform an AND bitwise operation between this BitArray and the one you pass into the function and return the result. Both BitArrays must have the same capacity. This will not change current BitArray values.
        /// </summary>
        /// <param name="other">BitArray with which to the And operation.</param>
        /// <returns>The resulting bit array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        T BitAnd(T other);

        /// <summary>
        /// Perform an OR bitwise operation between this BitArray and the one you pass into the function and return the result. Both BitArrays must have the same capacity. This will not change current BitArray values.
        /// </summary>
        /// <param name="other">BitArray with which to the Or operation.</param>
        /// <returns>The resulting bit array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        T BitOr(T other);

        /// <summary>
        /// Return the BitArray with every bit inverted.
        /// </summary>
        /// <returns>The resulting bit array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        T BitNot();

        /// <summary>
        /// Returns the amount of bits set to 1
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int CountBits();
    }

    // /!\ Important for serialization:
    // Serialization helper will rely on the name of the struct type.
    // In order to work, it must be BitArrayN where N is the capacity without suffix.

    /// <summary>
    /// Bit array of size 8.
    /// </summary>
    [Serializable]
    [System.Diagnostics.DebuggerDisplay("{this.GetType().Name} {HumanizedData}")]
    public struct BitArray8 : IBitArray<BitArray8>
    {
        [SerializeField] private byte _data;

        /// <summary>Number of elements in the bit array.</summary>
        public uint Capacity => 8u;

        /// <summary>True if all bits are 0.</summary>
        public bool AllFalse => _data == 0u;

        /// <summary>True if all bits are 1.</summary>
        public bool AllTrue => _data == byte.MaxValue;

        public byte Value => _data;

        /// <summary>Returns the bit array in a human-readable form.</summary>
        public string HumanizedData => $"{{{Convert.ToString(_data, 2)}, " + Capacity + "}}}".Replace(' ', '0');

        /// <summary>
        /// Returns the state of the bit at a specific index.
        /// </summary>
        /// <param name="index">Index of the bit.</param>
        /// <value>State of the bit at the provided index.</value>
        public bool this[uint index]
        {
            get => BitArrayUtilities.Get8(index, _data);
            set => BitArrayUtilities.Set8(index, ref _data, value);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="initValue">Initialization value.</param>
        public BitArray8(byte initValue)
        {
            _data = initValue;
        }


        /// <summary>
        /// Bit-wise Not operator
        /// </summary>
        /// <param name="a">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray8 operator ~(BitArray8 a) => new((byte)~a._data);

        /// <summary>
        /// Bit-wise Or operator
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray8 operator |(BitArray8 a, BitArray8 b) => new((byte)(a._data | b._data));

        /// <summary>
        /// Bit-wise And operator
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray8 operator &(BitArray8 a, BitArray8 b) => new((byte)(a._data & b._data));

        /// <summary>
        /// Bit-wise And
        /// </summary>
        /// <param name="other">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public BitArray8 BitAnd(BitArray8 other) => this & other;

        /// <summary>
        /// Bit-wise Or
        /// </summary>
        /// <param name="other">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public BitArray8 BitOr(BitArray8 other) => this | other;

        /// <summary>
        /// Bit-wise Not
        /// </summary>
        /// <returns>The resulting bit array.</returns>
        public BitArray8 BitNot() => ~this;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int CountBits() => math.countbits((ulong)_data);

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>True if both bit arrays are equals.</returns>
        public static bool operator ==(BitArray8 a, BitArray8 b) => a._data == b._data;

        /// <summary>
        /// Inequality operator.
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>True if the bit arrays are not equals.</returns>
        public static bool operator !=(BitArray8 a, BitArray8 b) => a._data != b._data;

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="obj">Bit array to compare to.</param>
        /// <returns>True if the provided bit array is equal to this.</returns>
        public override bool Equals(object obj) => obj is BitArray8 ba8 && ba8._data == _data;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public readonly bool Equals(BitArray8 other) => _data == other._data;

        /// <summary>
        /// Get the hashcode of the bit array.
        /// </summary>
        /// <returns>Hashcode of the bit array.</returns>
        public override int GetHashCode() => 1768953197 + _data.GetHashCode();
    }

    /// <summary>
    /// Bit array of size 16.
    /// </summary>
    [Serializable]
    [System.Diagnostics.DebuggerDisplay("{this.GetType().Name} {HumanizedData}")]
    public struct BitArray16 : IBitArray<BitArray16>
    {
        [SerializeField] private ushort _data;

        /// <summary>Number of elements in the bit array.</summary>
        public uint Capacity => 16u;

        /// <summary>True if all bits are 0.</summary>
        public bool AllFalse => _data == 0u;

        /// <summary>True if all bits are 1.</summary>
        public bool AllTrue => _data == ushort.MaxValue;

        public ushort Value => _data;

        /// <summary>Returns the bit array in a human-readable form.</summary>
        public string HumanizedData => System.Text.RegularExpressions.Regex
                                             .Replace($"{{{Convert.ToString(_data, 2)}, {Capacity}}}".Replace(' ', '0'), ".{8}", "$0.").TrimEnd('.');

        /// <summary>
        /// Returns the state of the bit at a specific index.
        /// </summary>
        /// <param name="index">Index of the bit.</param>
        /// <value>State of the bit at the provided index.</value>
        public bool this[uint index]
        {
            get => BitArrayUtilities.Get16(index, _data);
            set => BitArrayUtilities.Set16(index, ref _data, value);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="initValue">Initialization value.</param>
        public BitArray16(ushort initValue)
        {
            _data = initValue;
        }

        /// <summary>
        /// Bit-wise Not operator
        /// </summary>
        /// <param name="a">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray16 operator ~(BitArray16 a) => new((ushort)~a._data);

        /// <summary>
        /// Bit-wise Or operator
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray16 operator |(BitArray16 a, BitArray16 b) => new((ushort)(a._data | b._data));

        /// <summary>
        /// Bit-wise And operator
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray16 operator &(BitArray16 a, BitArray16 b) => new((ushort)(a._data & b._data));

        /// <summary>
        /// Bit-wise And
        /// </summary>
        /// <param name="other">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public BitArray16 BitAnd(BitArray16 other) => this & other;

        /// <summary>
        /// Bit-wise Or
        /// </summary>
        /// <param name="other">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public BitArray16 BitOr(BitArray16 other) => this | other;

        /// <summary>
        /// Bit-wise Not
        /// </summary>
        /// <returns>The resulting bit array.</returns>
        public BitArray16 BitNot() => ~this;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int CountBits() => math.countbits((ulong)_data);

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>True if both bit arrays are equals.</returns>
        public static bool operator ==(BitArray16 a, BitArray16 b) => a._data == b._data;

        /// <summary>
        /// Inequality operator.
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>True if the bit arrays are not equals.</returns>
        public static bool operator !=(BitArray16 a, BitArray16 b) => a._data != b._data;

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="obj">Bit array to compare to.</param>
        /// <returns>True if the provided bit array is equal to this.</returns>
        public override bool Equals(object obj) => obj is BitArray16 ba16 && ba16._data == _data;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public readonly bool Equals(BitArray16 other) => _data == other._data;

        /// <summary>
        /// Get the hashcode of the bit array.
        /// </summary>
        /// <returns>Hashcode of the bit array.</returns>
        public override int GetHashCode() => 1768953197 + _data.GetHashCode();
    }

    /// <summary>
    /// Bit array of size 32.
    /// </summary>
    [Serializable]
    [System.Diagnostics.DebuggerDisplay("{this.GetType().Name} {HumanizedData}")]
    public struct BitArray32 : IBitArray<BitArray32>
    {
        [SerializeField] private uint _data;

        /// <summary>Number of elements in the bit array.</summary>
        public uint Capacity => 32u;

        /// <summary>True if all bits are 0.</summary>
        public bool AllFalse => _data == 0u;

        /// <summary>True if all bits are 1.</summary>
        public bool AllTrue => _data == uint.MaxValue;

        public uint Value => _data;

        /// <summary>Returns the bit array in a human-readable form.</summary>
        public string HumanizedData => System.Text.RegularExpressions.Regex
                                             .Replace($"{{{Convert.ToString(_data, 2)}, {Capacity}}}".Replace(' ', '0'), ".{8}", "$0.").TrimEnd('.');

        /// <summary>
        /// Returns the state of the bit at a specific index.
        /// </summary>
        /// <param name="index">Index of the bit.</param>
        /// <value>State of the bit at the provided index.</value>
        public bool this[uint index]
        {
            get => BitArrayUtilities.Get32(index, _data);
            set => BitArrayUtilities.Set32(index, ref _data, value);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="initValue">Initialization value.</param>
        public BitArray32(uint initValue)
        {
            _data = initValue;
        }

        /// <summary>
        /// Bit-wise And
        /// </summary>
        /// <param name="other">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public BitArray32 BitAnd(BitArray32 other) => this & (BitArray32)other;

        /// <summary>
        /// Bit-wise Or
        /// </summary>
        /// <param name="other">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public BitArray32 BitOr(BitArray32 other) => this | (BitArray32)other;

        /// <summary>
        /// Bit-wise Not
        /// </summary>
        /// <returns>The resulting bit array.</returns>
        public BitArray32 BitNot() => ~this;

        /// <summary>
        /// Bit-wise Not operator
        /// </summary>
        /// <param name="a">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray32 operator ~(BitArray32 a) => new(~a._data);

        /// <summary>
        /// Bit-wise Or operator
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray32 operator |(BitArray32 a, BitArray32 b) => new(a._data | b._data);

        /// <summary>
        /// Bit-wise And operator
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray32 operator &(BitArray32 a, BitArray32 b) => new(a._data & b._data);

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>True if both bit arrays are equals.</returns>
        public static bool operator ==(BitArray32 a, BitArray32 b) => a._data == b._data;

        /// <summary>
        /// Inequality operator.
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>True if the bit arrays are not equals.</returns>
        public static bool operator !=(BitArray32 a, BitArray32 b) => a._data != b._data;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int CountBits() => math.countbits((uint)_data);

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="obj">Bit array to compare to.</param>
        /// <returns>True if the provided bit array is equal to this.</returns>
        public override bool Equals(object obj) => obj is BitArray32 ba32 && ba32._data == _data;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public readonly bool Equals(BitArray32 other) => _data == other._data;

        /// <summary>
        /// Get the hashcode of the bit array.
        /// </summary>
        /// <returns>Hashcode of the bit array.</returns>
        public override int GetHashCode() => 1768953197 + _data.GetHashCode();
    }

    /// <summary>
    /// Bit array of size 64.
    /// </summary>
    [Serializable]
    [System.Diagnostics.DebuggerDisplay("{this.GetType().Name} {HumanizedData}")]
    public struct BitArray64 : IBitArray<BitArray64>
    {
        [SerializeField] private ulong _data;

        /// <summary>Number of elements in the bit array.</summary>
        public uint Capacity => 64u;

        /// <summary>True if all bits are 0.</summary>
        public bool AllFalse => _data == 0uL;

        /// <summary>True if all bits are 1.</summary>
        public bool AllTrue => _data == ulong.MaxValue;

        public ulong Value => _data;

        /// <summary>Returns the bit array in a human-readable form.</summary>
        public string HumanizedData => System.Text.RegularExpressions.Regex
                                             .Replace($"{{{Convert.ToString((long)_data, 2)}, {Capacity}}}".Replace(' ', '0'), ".{8}", "$0.")
                                             .TrimEnd('.');

        /// <summary>
        /// Returns the state of the bit at a specific index.
        /// </summary>
        /// <param name="index">Index of the bit.</param>
        /// <value>State of the bit at the provided index.</value>
        public bool this[uint index]
        {
            get => BitArrayUtilities.Get64(index, _data);
            set => BitArrayUtilities.Set64(index, ref _data, value);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="initValue">Initialization value.</param>
        public BitArray64(ulong initValue)
        {
            _data = initValue;
        }

        /// <summary>
        /// Bit-wise Not operator
        /// </summary>
        /// <param name="a">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray64 operator ~(BitArray64 a) => new(~a._data);

        /// <summary>
        /// Bit-wise Or operator
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray64 operator |(BitArray64 a, BitArray64 b) => new(a._data | b._data);

        /// <summary>
        /// Bit-wise And operator
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray64 operator &(BitArray64 a, BitArray64 b) => new(a._data & b._data);

        /// <summary>
        /// Bit-wise And
        /// </summary>
        /// <param name="other">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public BitArray64 BitAnd(BitArray64 other) => this & other;

        /// <summary>
        /// Bit-wise Or
        /// </summary>
        /// <param name="other">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public BitArray64 BitOr(BitArray64 other) => this | other;

        /// <summary>
        /// Bit-wise Not
        /// </summary>
        /// <returns>The resulting bit array.</returns>
        public BitArray64 BitNot() => ~this;

        public int CountBits() => math.countbits(_data);

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>True if both bit arrays are equals.</returns>
        public static bool operator ==(BitArray64 a, BitArray64 b) => a._data == b._data;

        /// <summary>
        /// Inequality operator.
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>True if the bit arrays are not equals.</returns>
        public static bool operator !=(BitArray64 a, BitArray64 b) => a._data != b._data;

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="obj">Bit array to compare to.</param>
        /// <returns>True if the provided bit array is equal to this.</returns>
        public override bool Equals(object obj) => obj is BitArray64 ba64 && ba64._data == _data;

        public readonly bool Equals(BitArray64 other) => _data == other._data;

        /// <summary>
        /// Get the hashcode of the bit array.
        /// </summary>
        /// <returns>Hashcode of the bit array.</returns>
        public override int GetHashCode() => 1768953197 + _data.GetHashCode();
    }

    /// <summary>
    /// Bit array of size 128.
    /// </summary>
    [Serializable]
    [System.Diagnostics.DebuggerDisplay("{this.GetType().Name} {HumanizedData}")]
    public struct BitArray128 : IBitArray<BitArray128>
    {
        [SerializeField] private ulong _data1;
        [SerializeField] private ulong _data2;

        /// <summary>Number of elements in the bit array.</summary>
        public uint Capacity => 128u;

        /// <summary>True if all bits are 0.</summary>
        public bool AllFalse => _data1 == 0uL && _data2 == 0uL;

        /// <summary>True if all bits are 1.</summary>
        public bool AllTrue => _data1 == ulong.MaxValue && _data2 == ulong.MaxValue;

        public ulong Value1 => _data1;
        public ulong Value2 => _data2;

        /// <summary>Returns the bit array in a human-readable form.</summary>
        public string HumanizedData =>
            System.Text.RegularExpressions.Regex.Replace($"{{{Convert.ToString((long)_data2, 2)}, {64u}}}".Replace(' ', '0'), ".{8}", "$0.")
            + System.Text.RegularExpressions.Regex.Replace($"{{{Convert.ToString((long)_data1, 2)}, {64u}}}".Replace(' ', '0'), ".{8}", "$0.").TrimEnd('.');

        /// <summary>
        /// Returns the state of the bit at a specific index.
        /// </summary>
        /// <param name="index">Index of the bit.</param>
        /// <value>State of the bit at the provided index.</value>
        public bool this[uint index]
        {
            get => index < 64u
                ? (_data1 & (1uL << (int)index)) != 0uL
                : (_data2 & (1uL << (int)(index - 64u))) != 0uL;

            set
            {
                if (index < 64u)
                {
                    _data1 = value ? _data1 | (1uL << (int)index) : _data1 & ~(1uL << (int)index);
                }
                else
                {
                    _data2 = value ? _data2 | (1uL << (int)(index - 64u)) : _data2 & ~(1uL << (int)(index - 64u));
                }
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="initValue1">Initialization value 1.</param>
        /// <param name="initValue2">Initialization value 2.</param>
        public BitArray128(ulong initValue1, ulong initValue2)
        {
            _data1 = initValue1;
            _data2 = initValue2;
        }

        /// <summary>
        /// Bit-wise Not operator
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray128 operator ~(BitArray128 a) => new(~a._data1, ~a._data2);

        /// <summary>
        /// Bit-wise Or operator
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray128 operator |(BitArray128 a, BitArray128 b) => new(a._data1 | b._data1, a._data2 | b._data2);

        /// <summary>
        /// Bit-wise And operator
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray128 operator &(BitArray128 a, BitArray128 b) => new(a._data1 & b._data1, a._data2 & b._data2);

        /// <summary>
        /// Bit-wise And
        /// </summary>
        /// <param name="other">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public BitArray128 BitAnd(BitArray128 other) => this & other;

        /// <summary>
        /// Bit-wise Or
        /// </summary>
        /// <param name="other">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public BitArray128 BitOr(BitArray128 other) => this | other;

        /// <summary>
        /// Bit-wise Not
        /// </summary>
        /// <returns>The resulting bit array.</returns>
        public BitArray128 BitNot() => ~this;

        public int CountBits() => math.countbits(_data1) + math.countbits(_data2);

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>True if both bit arrays are equals.</returns>
        public static bool operator ==(BitArray128 a, BitArray128 b) => a._data1 == b._data1 && a._data2 == b._data2;

        /// <summary>
        /// Inequality operator.
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>True if the bit arrays are not equals.</returns>
        public static bool operator !=(BitArray128 a, BitArray128 b) => a._data1 != b._data1 || a._data2 != b._data2;

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="obj">Bit array to compare to.</param>
        /// <returns>True if the provided bit array is equal to this.</returns>
        public override bool Equals(object obj) => obj is BitArray128 ba128 && _data1.Equals(ba128._data1) && _data2.Equals(ba128._data2);

        public readonly bool Equals(BitArray128 other) => _data1 == other._data1 && _data2 == other._data2;

        /// <summary>
        /// Get the hashcode of the bit array.
        /// </summary>
        /// <returns>Hashcode of the bit array.</returns>
        public override int GetHashCode()
        {
            var hashCode = 1755735569;
            hashCode = hashCode * -1521134295 + _data1.GetHashCode();
            hashCode = hashCode * -1521134295 + _data2.GetHashCode();
            return hashCode;
        }
    }

    /// <summary>
    /// Bit array of size 256.
    /// </summary>
    [Serializable]
    [System.Diagnostics.DebuggerDisplay("{this.GetType().Name} {HumanizedData}")]
    public struct BitArray256 : IBitArray<BitArray256>
    {
        [SerializeField] private ulong _data1;
        [SerializeField] private ulong _data2;
        [SerializeField] private ulong _data3;
        [SerializeField] private ulong _data4;

        /// <summary>Number of elements in the bit array.</summary>
        public uint Capacity => 256u;

        /// <summary>True if all bits are 0.</summary>
        public bool AllFalse => _data1 == 0uL && _data2 == 0uL && _data3 == 0uL && _data4 == 0uL;

        /// <summary>True if all bits are 1.</summary>
        public bool AllTrue => _data1 == ulong.MaxValue && _data2 == ulong.MaxValue && _data3 == ulong.MaxValue && _data4 == ulong.MaxValue;

        public ulong Value1 => _data1;
        public ulong Value2 => _data2;
        public ulong Value3 => _data3;
        public ulong Value4 => _data4;

        /// <summary>Returns the bit array in a human-readable form.</summary>
        public string HumanizedData =>
            System.Text.RegularExpressions.Regex.Replace($"{Convert.ToString((long)_data4, 2)}, {64u}}}".Replace(' ', '0'), ".{8}", "$0.")
            + System.Text.RegularExpressions.Regex.Replace($"{Convert.ToString((long)_data3, 2)}, {64u}}}".Replace(' ', '0'), ".{8}", "$0.")
            + System.Text.RegularExpressions.Regex.Replace($"{Convert.ToString((long)_data2, 2)}, {64u}}}".Replace(' ', '0'), ".{8}", "$0.")
            + System.Text.RegularExpressions.Regex.Replace($"{Convert.ToString((long)_data1, 2)}, {64u}}}".Replace(' ', '0'), ".{8}", "$0.").TrimEnd('.');

        /// <summary>
        /// Returns the state of the bit at a specific index.
        /// </summary>
        /// <param name="index">Index of the bit.</param>
        /// <value>State of the bit at the provided index.</value>
        public bool this[uint index]
        {
            get => BitArrayUtilities.Get256(index, _data1, _data2, _data3, _data4);
            set => BitArrayUtilities.Set256(index, ref _data1, ref _data2, ref _data3, ref _data4, value);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="initValue1">Initialization value 1.</param>
        /// <param name="initValue2">Initialization value 2.</param>
        /// <param name="initValue3">Initialization value 3.</param>
        /// <param name="initValue4">Initialization value 4.</param>
        public BitArray256(ulong initValue1, ulong initValue2, ulong initValue3, ulong initValue4)
        {
            _data1 = initValue1;
            _data2 = initValue2;
            _data3 = initValue3;
            _data4 = initValue4;
        }

        /// <summary>
        /// Bit-wise Not operator
        /// </summary>
        /// <param name="a">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray256 operator ~(BitArray256 a) => new(~a._data1, ~a._data2, ~a._data3, ~a._data4);

        /// <summary>
        /// Bit-wise Or operator
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray256 operator |(BitArray256 a, BitArray256 b) =>
            new(a._data1 | b._data1, a._data2 | b._data2, a._data3 | b._data3, a._data4 | b._data4);

        /// <summary>
        /// Bit-wise And operator
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>The resulting bit array.</returns>
        public static BitArray256 operator &(BitArray256 a, BitArray256 b) =>
            new(a._data1 & b._data1, a._data2 & b._data2, a._data3 & b._data3, a._data4 & b._data4);

        /// <summary>
        /// Bit-wise And
        /// </summary>
        /// <param name="other">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public BitArray256 BitAnd(BitArray256 other) => this & other;

        /// <summary>
        /// Bit-wise Or
        /// </summary>
        /// <param name="other">Bit array with which to do the operation.</param>
        /// <returns>The resulting bit array.</returns>
        public BitArray256 BitOr(BitArray256 other) => this | other;

        /// <summary>
        /// Bit-wise Not
        /// </summary>
        /// <returns>The resulting bit array.</returns>
        public BitArray256 BitNot() => ~this;

        public int CountBits() => math.countbits(_data1) + math.countbits(_data2) + math.countbits(_data3) + math.countbits(_data4);

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>True if both bit arrays are equals.</returns>
        public static bool operator ==(BitArray256 a, BitArray256 b) =>
            a._data1 == b._data1 && a._data2 == b._data2 && a._data3 == b._data3 && a._data4 == b._data4;

        /// <summary>
        /// Inequality operator.
        /// </summary>
        /// <param name="a">First bit array.</param>
        /// <param name="b">Second bit array.</param>
        /// <returns>True if the bit arrays are not equals.</returns>
        public static bool operator !=(BitArray256 a, BitArray256 b) =>
            a._data1 != b._data1 || a._data2 != b._data2 || a._data3 != b._data3 || a._data4 != b._data4;

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="obj">Bit array to compare to.</param>
        /// <returns>True if the provided bit array is equal to this.</returns>
        public override bool Equals(object obj)
            => obj is BitArray256 ba256
               && _data1.Equals(ba256._data1)
               && _data2.Equals(ba256._data2)
               && _data3.Equals(ba256._data3)
               && _data4.Equals(ba256._data4);

        public readonly bool Equals(BitArray256 other) => _data1 == other._data1 && _data2 == other._data2 && _data3 == other._data3 && _data4 == other._data4;

        /// <summary>
        /// Get the hashcode of the bit array.
        /// </summary>
        /// <returns>Hashcode of the bit array.</returns>
        public override int GetHashCode()
        {
            var hashCode = 1870826326;
            hashCode = hashCode * -1521134295 + _data1.GetHashCode();
            hashCode = hashCode * -1521134295 + _data2.GetHashCode();
            hashCode = hashCode * -1521134295 + _data3.GetHashCode();
            hashCode = hashCode * -1521134295 + _data4.GetHashCode();
            return hashCode;
        }
    }

    /// <summary>
    /// Bit array utility class.
    /// </summary>
    public static class BitArrayUtilities
    {
        //written here to not duplicate the serialized accessor and runtime accessor

        /// <summary>
        /// Get a bit at a specific index.
        /// </summary>
        /// <param name="index">Bit index.</param>
        /// <param name="data">Bit array data.</param>
        /// <returns>The value of the bit at the specific index.</returns>
        public static bool Get8(uint index, byte data) => (data & (1u << (int)index)) != 0u;

        /// <summary>
        /// Get a bit at a specific index.
        /// </summary>
        /// <param name="index">Bit index.</param>
        /// <param name="data">Bit array data.</param>
        /// <returns>The value of the bit at the specific index.</returns>
        public static bool Get16(uint index, ushort data) => (data & (1u << (int)index)) != 0u;

        /// <summary>
        /// Get a bit at a specific index.
        /// </summary>
        /// <param name="index">Bit index.</param>
        /// <param name="data">Bit array data.</param>
        /// <returns>The value of the bit at the specific index.</returns>
        public static bool Get32(uint index, uint data) => (data & (1u << (int)index)) != 0u;

        /// <summary>
        /// Get a bit at a specific index.
        /// </summary>
        /// <param name="index">Bit index.</param>
        /// <param name="data">Bit array data.</param>
        /// <returns>The value of the bit at the specific index.</returns>
        public static bool Get64(uint index, ulong data) => (data & (1uL << (int)index)) != 0uL;

        /// <summary>
        /// Get a bit at a specific index.
        /// </summary>
        /// <param name="index">Bit index.</param>
        /// <param name="data1">Bit array data 1.</param>
        /// <param name="data2">Bit array data 2.</param>
        /// <returns>The value of the bit at the specific index.</returns>
        public static bool Get128(uint index, ulong data1, ulong data2)
            => index < 64u
                ? (data1 & (1uL << (int)index)) != 0uL
                : (data2 & (1uL << (int)(index - 64u))) != 0uL;

        /// <summary>
        /// Get a bit at a specific index.
        /// </summary>
        /// <param name="index">Bit index.</param>
        /// <param name="data1">Bit array data 1.</param>
        /// <param name="data2">Bit array data 2.</param>
        /// <param name="data3">Bit array data 3.</param>
        /// <param name="data4">Bit array data 4.</param>
        /// <returns>The value of the bit at the specific index.</returns>
        public static bool Get256(uint index, ulong data1, ulong data2, ulong data3, ulong data4)
            => index < 128u
                ? index < 64u
                    ? (data1 & (1uL << (int)index)) != 0uL
                    : (data2 & (1uL << (int)(index - 64u))) != 0uL
                : index < 192u
                    ? (data3 & (1uL << (int)(index - 128u))) != 0uL
                    : (data4 & (1uL << (int)(index - 192u))) != 0uL;

        /// <summary>
        /// Set a bit at a specific index.
        /// </summary>
        /// <param name="index">Bit index.</param>
        /// <param name="data">Bit array data.</param>
        /// <param name="value">Value to set the bit to.</param>
        public static void Set8(uint index, ref byte data, bool value) => data = (byte)(value ? data | (1u << (int)index) : data & ~(1u << (int)index));

        /// <summary>
        /// Set a bit at a specific index.
        /// </summary>
        /// <param name="index">Bit index.</param>
        /// <param name="data">Bit array data.</param>
        /// <param name="value">Value to set the bit to.</param>
        public static void Set16(uint index, ref ushort data, bool value) => data = (ushort)(value ? data | (1u << (int)index) : data & ~(1u << (int)index));

        /// <summary>
        /// Set a bit at a specific index.
        /// </summary>
        /// <param name="index">Bit index.</param>
        /// <param name="data">Bit array data.</param>
        /// <param name="value">Value to set the bit to.</param>
        public static void Set32(uint index, ref uint data, bool value) => data = value ? data | (1u << (int)index) : data & ~(1u << (int)index);

        /// <summary>
        /// Set a bit at a specific index.
        /// </summary>
        /// <param name="index">Bit index.</param>
        /// <param name="data">Bit array data.</param>
        /// <param name="value">Value to set the bit to.</param>
        public static void Set64(uint index, ref ulong data, bool value) => data = value ? data | (1uL << (int)index) : data & ~(1uL << (int)index);

        /// <summary>
        /// Set a bit at a specific index.
        /// </summary>
        /// <param name="index">Bit index.</param>
        /// <param name="data1">Bit array data 1.</param>
        /// <param name="data2">Bit array data 2.</param>
        /// <param name="value">Value to set the bit to.</param>
        public static void Set128(uint index, ref ulong data1, ref ulong data2, bool value)
        {
            if (index < 64u)
            {
                data1 = value ? data1 | (1uL << (int)index) : data1 & ~(1uL << (int)index);
            }
            else
            {
                data2 = value ? data2 | (1uL << (int)(index - 64u)) : data2 & ~(1uL << (int)(index - 64u));
            }
        }

        /// <summary>
        /// Set a bit at a specific index.
        /// </summary>
        /// <param name="index">Bit index.</param>
        /// <param name="data1">Bit array data 1.</param>
        /// <param name="data2">Bit array data 2.</param>
        /// <param name="data3">Bit array data 3.</param>
        /// <param name="data4">Bit array data 4.</param>
        /// <param name="value">Value to set the bit to.</param>
        public static void Set256(uint index, ref ulong data1, ref ulong data2, ref ulong data3, ref ulong data4, bool value)
        {
            if (index < 64u)
            {
                data1 = value ? data1 | (1uL << (int)index) : data1 & ~(1uL << (int)index);
            }
            else if (index < 128u)
            {
                data2 = value ? data2 | (1uL << (int)(index - 64u)) : data2 & ~(1uL << (int)(index - 64u));
            }
            else if (index < 192u)
            {
                data3 = value ? data3 | (1uL << (int)(index - 64u)) : data3 & ~(1uL << (int)(index - 128u));
            }
            else
            {
                data4 = value ? data4 | (1uL << (int)(index - 64u)) : data4 & ~(1uL << (int)(index - 192u));
            }
        }
    }
}