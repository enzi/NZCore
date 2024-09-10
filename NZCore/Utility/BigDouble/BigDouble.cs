// <copyright project="NZCore" file="BigDouble.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Globalization;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace NZCore
{
    public struct BigDouble : IFormattable, IComparable, IComparable<BigDouble>, IEquatable<BigDouble>
    {
        public const double Tolerance = 1e-18;

        //for example: if two exponents are more than 17 apart, consider adding them together pointless, just return the larger one
        internal const int MaxSignificantDigits = 17;

        internal const long ExpLimit = long.MaxValue;

        //the largest exponent that can appear in a Double, though not all mantissas are valid here.
        internal const long DoubleExpMax = 308;

        //The smallest exponent that can appear in a Double, though not all mantissas are valid here.
        internal const long DoubleExpMin = -324;

        internal readonly double mantissa;
        internal readonly long exponent;

        public double Mantissa => mantissa;
        public long Exponent => exponent;

        public readonly static BigDouble Zero = FromMantissaExponentNoNormalize(0, 0);
        public readonly static BigDouble One = FromMantissaExponentNoNormalize(1, 0);
        public readonly static BigDouble NaN = FromMantissaExponentNoNormalize(double.NaN, long.MinValue);
        public readonly static BigDouble PositiveInfinity = FromMantissaExponentNoNormalize(double.PositiveInfinity, 0);
        public readonly static BigDouble NegativeInfinity = FromMantissaExponentNoNormalize(double.NegativeInfinity, 0);
        public static bool IsNaN(BigDouble value) => double.IsNaN(value.Mantissa);
        public static bool IsPositiveInfinity(BigDouble value) => double.IsPositiveInfinity(value.Mantissa);
        public static bool IsNegativeInfinity(BigDouble value) => double.IsNegativeInfinity(value.Mantissa);
        public static bool IsInfinity(BigDouble value) => double.IsInfinity(value.Mantissa);
        public static BigDouble FromMantissaExponentNoNormalize(double mantissa, long exponent) => new BigDouble(mantissa, exponent);


        private BigDouble(double mantissa, long exponent)
        {
            this.mantissa = mantissa;
            this.exponent = exponent;
        }

        // public BigDouble(double mantissa, long exponent)
        // {
        //     this = Normalize(mantissa, exponent);
        // }

        public BigDouble(BigDouble other)
        {
            mantissa = other.mantissa;
            exponent = other.exponent;
        }

        public BigDouble(double value)
        {
            //SAFETY: Handle Infinity and NaN in a somewhat meaningful way.
            if (double.IsNaN(value))
            {
                this = NaN;
            }
            else if (double.IsPositiveInfinity(value))
            {
                this = PositiveInfinity;
            }
            else if (double.IsNegativeInfinity(value))
            {
                this = NegativeInfinity;
            }
            else if (IsZero(value))
            {
                this = Zero;
            }
            else
            {
                this = Normalize(value, 0);
            }
        }

        public static BigDouble Normalize(double mantissa, long exponent)
        {
            if (mantissa >= 1 && mantissa < 10 || !IsFinite(mantissa))
            {
                return FromMantissaExponentNoNormalize(mantissa, exponent);
            }

            if (IsZero(mantissa))
            {
                return Zero;
            }

            var tempExponent = (long)math.floor(math.log10(math.abs(mantissa)));
            //SAFETY: handle 5e-324, -5e-324 separately
            if (tempExponent == DoubleExpMin)
            {
                mantissa = mantissa * 10 / 1e-323;
            }
            else
            {
                mantissa = mantissa / PowersOf10.Lookup(tempExponent);
            }

            return FromMantissaExponentNoNormalize(mantissa, exponent + tempExponent);
        }

        [BurstDiscard]
        public static BigDouble Parse(string value)
        {
            if (value.IndexOf('e') != -1)
            {
                var parts = value.Split('e');
                var mantissa = double.Parse(parts[0], CultureInfo.InvariantCulture);
                var exponent = long.Parse(parts[1], CultureInfo.InvariantCulture);
                return Normalize(mantissa, exponent);
            }

            if (value == "NaN")
            {
                return NaN;
            }

            var result = new BigDouble(double.Parse(value, CultureInfo.InvariantCulture));
            if (IsNaN(result))
            {
                throw new Exception("Invalid argument: " + value);
            }

            return result;
        }

        public double ToDouble()
        {
            if (IsNaN(this))
            {
                return double.NaN;
            }

            switch (Exponent)
            {
                case > DoubleExpMax:
                    return Mantissa > 0 ? double.PositiveInfinity : double.NegativeInfinity;
                case < DoubleExpMin:
                    return 0.0;
                //SAFETY: again, handle 5e-324, -5e-324 separately
                case DoubleExpMin:
                    return Mantissa > 0 ? 5e-324 : -5e-324;
            }

            var result = Mantissa * PowersOf10.Lookup(Exponent);
            if (!IsFinite(result) || Exponent < 0)
            {
                return result;
            }

            var resultrounded = math.round(result);
            return math.abs(resultrounded - result) < 1e-10 ? resultrounded : result;
        }

        public override string ToString() => BigNumber.FormatBigDouble(this, null, null);

        public string ToString(string format) => BigNumber.FormatBigDouble(this, format, null);

        public string ToString(string format, IFormatProvider formatProvider) => BigNumber.FormatBigDouble(this, format, formatProvider);

        public static BigDouble Abs(BigDouble value) => FromMantissaExponentNoNormalize(math.abs(value.Mantissa), value.Exponent);

        public static BigDouble Negate(BigDouble value) => FromMantissaExponentNoNormalize(-value.Mantissa, value.Exponent);

        public static int Sign(BigDouble value) => (int)math.sign(value.Mantissa);

        public static BigDouble Round(BigDouble value)
        {
            if (IsNaN(value))
            {
                return value;
            }

            switch (value.Exponent)
            {
                case < -1:
                    return Zero;
                case < MaxSignificantDigits:
                    return new BigDouble(math.round(value.ToDouble()));
                default:
                    return value;
            }
        }

        // public static BigDouble Round(BigDouble value, MidpointRounding mode)
        // {
        //     if (IsNaN(value))
        //     {
        //         return value;
        //     }
        //
        //     switch (value.Exponent)
        //     {
        //         case < -1:
        //             return Zero;
        //         case < MaxSignificantDigits:
        //             return new BigDouble(math.round(value.ToDouble(), mode));
        //         default:
        //             return value;
        //     }
        // }

        public static BigDouble Floor(BigDouble value)
        {
            if (IsNaN(value))
            {
                return value;
            }

            switch (value.Exponent)
            {
                case < -1:
                    return math.sign(value.Mantissa) >= 0 ? Zero : -One;
                case < MaxSignificantDigits:
                    return new BigDouble(math.floor(value.ToDouble()));
                default:
                    return value;
            }
        }

        public static BigDouble Ceiling(BigDouble value)
        {
            if (IsNaN(value))
            {
                return value;
            }

            switch (value.Exponent)
            {
                case < -1:
                    return math.sign(value.Mantissa) > 0 ? One : Zero;
                case < MaxSignificantDigits:
                    return new BigDouble(math.ceil(value.ToDouble()));
                default:
                    return value;
            }
        }

        public static BigDouble Truncate(BigDouble value)
        {
            if (IsNaN(value))
            {
                return value;
            }

            switch (value.Exponent)
            {
                case < 0:
                    return Zero;
                case < MaxSignificantDigits:
                    return new BigDouble(math.trunc(value.ToDouble()));
                default:
                    return value;
            }
        }

        public static BigDouble Add(BigDouble left, BigDouble right)
        {
            //figure out which is bigger, shrink the mantissa of the smaller by the difference in exponents, add mantissas, normalize and return

            //TODO: Optimizations and simplification may be possible, see https://github.com/Patashu/break_infinity.js/issues/8

            if (IsZero(left.Mantissa))
            {
                return right;
            }

            if (IsZero(right.Mantissa))
            {
                return left;
            }

            if (IsNaN(left) || IsNaN(right) || IsInfinity(left) || IsInfinity(right))
            {
                // Let Double handle these cases.
                return left.Mantissa + right.Mantissa;
            }

            BigDouble bigger, smaller;
            if (left.Exponent >= right.Exponent)
            {
                bigger = left;
                smaller = right;
            }
            else
            {
                bigger = right;
                smaller = left;
            }

            if (bigger.Exponent - smaller.Exponent > MaxSignificantDigits)
            {
                return bigger;
            }

            //have to do this because adding numbers that were once integers but scaled down is imprecise.
            //Example: 299 + 18
            return Normalize(math.round(1e14 * bigger.Mantissa + 1e14 * smaller.Mantissa * PowersOf10.Lookup(smaller.Exponent - bigger.Exponent)), bigger.Exponent - 14);
        }

        public static BigDouble Subtract(BigDouble left, BigDouble right)
        {
            return left + -right;
        }

        public static BigDouble Multiply(BigDouble left, BigDouble right)
        {
            // 2e3 * 4e5 = (2 * 4)e(3 + 5)
            return Normalize(left.Mantissa * right.Mantissa, left.Exponent + right.Exponent);
        }

        public static BigDouble Divide(BigDouble left, BigDouble right)
        {
            return left * Reciprocate(right);
        }

        public static BigDouble Reciprocate(BigDouble value)
        {
            return Normalize(1.0 / value.Mantissa, -value.Exponent);
        }

        public static implicit operator BigDouble(double value)
        {
            return new BigDouble(value);
        }

        public static implicit operator BigDouble(int value)
        {
            return new BigDouble(value);
        }

        public static implicit operator BigDouble(long value)
        {
            return new BigDouble(value);
        }

        public static implicit operator BigDouble(float value)
        {
            return new BigDouble(value);
        }

        public static BigDouble operator -(BigDouble value)
        {
            return Negate(value);
        }

        public static BigDouble operator +(BigDouble left, BigDouble right)
        {
            return Add(left, right);
        }

        public static BigDouble operator -(BigDouble left, BigDouble right)
        {
            return Subtract(left, right);
        }

        public static BigDouble operator *(BigDouble left, BigDouble right)
        {
            return Multiply(left, right);
        }

        public static BigDouble operator /(BigDouble left, BigDouble right)
        {
            return Divide(left, right);
        }

        public static BigDouble operator ++(BigDouble value)
        {
            return value.Add(1);
        }

        public static BigDouble operator --(BigDouble value)
        {
            return value.Subtract(1);
        }

        public int CompareTo(object other)
        {
            switch (other)
            {
                case null:
                    return 1;
                case BigDouble bigDouble:
                    return CompareTo(bigDouble);
                default:
                    throw new ArgumentException("The parameter must be a BigDouble.");
            }
        }

        public int CompareTo(BigDouble other)
        {
            if (
                IsZero(Mantissa) || IsZero(other.Mantissa)
                                 || IsNaN(this) || IsNaN(other)
                                 || IsInfinity(this) || IsInfinity(other))
            {
                // Let Double handle these cases.
                return Mantissa.CompareTo(other.Mantissa);
            }

            switch (Mantissa)
            {
                case > 0 when other.Mantissa < 0:
                    return 1;
                case < 0 when other.Mantissa > 0:
                    return -1;
                default:
                {
                    var exponentComparison = Exponent.CompareTo(other.Exponent);
                    return exponentComparison != 0
                        ? (Mantissa > 0 ? exponentComparison : -exponentComparison)
                        : Mantissa.CompareTo(other.Mantissa);
                }
            }
        }

        public override bool Equals(object other)
        {
            return other is BigDouble bigDouble && Equals(bigDouble);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Mantissa.GetHashCode() * 397) ^ Exponent.GetHashCode();
            }
        }

        public bool Equals(BigDouble other)
        {
            return !IsNaN(this) && !IsNaN(other) && (AreSameInfinity(this, other)
                                                     || Exponent == other.Exponent && AreEqual(Mantissa, other.Mantissa));
        }

        public bool Equals(BigDouble other, double tolerance)
        {
            return !IsNaN(this) && !IsNaN(other) && (AreSameInfinity(this, other)
                                                     || Abs(this - other) <= Max(Abs(this), Abs(other)) * tolerance);
        }

        private static bool AreSameInfinity(BigDouble first, BigDouble second)
        {
            return IsPositiveInfinity(first) && IsPositiveInfinity(second)
                   || IsNegativeInfinity(first) && IsNegativeInfinity(second);
        }

        public static bool operator ==(BigDouble left, BigDouble right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BigDouble left, BigDouble right)
        {
            return !(left == right);
        }

        public static bool operator <(BigDouble a, BigDouble b)
        {
            if (IsNaN(a) || IsNaN(b))
            {
                return false;
            }

            if (IsZero(a.Mantissa)) return b.Mantissa > 0;
            if (IsZero(b.Mantissa)) return a.Mantissa < 0;
            if (a.Exponent == b.Exponent) return a.Mantissa < b.Mantissa;
            if (a.Mantissa > 0) return b.Mantissa > 0 && a.Exponent < b.Exponent;
            return b.Mantissa > 0 || a.Exponent > b.Exponent;
        }

        public static bool operator <=(BigDouble a, BigDouble b)
        {
            if (IsNaN(a) || IsNaN(b))
            {
                return false;
            }

            return !(a > b);
        }

        public static bool operator >(BigDouble a, BigDouble b)
        {
            if (IsNaN(a) || IsNaN(b))
            {
                return false;
            }

            if (IsZero(a.Mantissa)) return b.Mantissa < 0;
            if (IsZero(b.Mantissa)) return a.Mantissa > 0;
            if (a.Exponent == b.Exponent) return a.Mantissa > b.Mantissa;
            if (a.Mantissa > 0) return b.Mantissa < 0 || a.Exponent > b.Exponent;
            return b.Mantissa < 0 && a.Exponent < b.Exponent;
        }

        public static bool operator >=(BigDouble a, BigDouble b)
        {
            if (IsNaN(a) || IsNaN(b))
            {
                return false;
            }

            return !(a < b);
        }

        public static BigDouble Max(BigDouble left, BigDouble right)
        {
            if (IsNaN(left) || IsNaN(right))
            {
                return NaN;
            }

            return left > right ? left : right;
        }

        public static BigDouble Min(BigDouble left, BigDouble right)
        {
            if (IsNaN(left) || IsNaN(right))
            {
                return NaN;
            }

            return left > right ? right : left;
        }

        public static double AbsLog10(BigDouble value)
        {
            return value.Exponent + math.log10(math.abs(value.Mantissa));
        }

        public static double Log10(BigDouble value)
        {
            return value.Exponent + math.log10(value.Mantissa);
        }

        public static double Log(BigDouble value, BigDouble @base)
        {
            return Log(value, @base.ToDouble());
        }

        public static double Log(BigDouble value, double @base)
        {
            if (IsZero(@base))
            {
                return double.NaN;
            }

            //UN-SAFETY: Most incremental game cases are log(number := 1 or greater, base := 2 or greater). We assume this to be true and thus only need to return a number, not a BigDouble, and don't do any other kind of error checking.
            return 2.30258509299404568402 / math.log(@base) * Log10(value);
        }

        public static double Log2(BigDouble value)
        {
            return 3.32192809488736234787 * Log10(value);
        }

        public static double Ln(BigDouble value)
        {
            return 2.30258509299404568402 * Log10(value);
        }

        public static BigDouble Pow10(double power)
        {
            return IsInteger(power)
                ? Pow10((long)power)
                : Normalize(math.pow(10, power % 1), (long)math.trunc(power));
        }

        public static BigDouble Pow10(long power)
        {
            return FromMantissaExponentNoNormalize(1, power);
        }

        public static BigDouble Pow(BigDouble value, BigDouble power)
        {
            return Pow(value, power.ToDouble());
        }

        public static BigDouble Pow(BigDouble value, long power)
        {
            if (Is10(value))
            {
                return Pow10(power);
            }

            var mantissa = math.pow(value.Mantissa, power);
            return double.IsInfinity(mantissa)
                ?
                // TODO: This is rather dumb, but works anyway
                // Power is too big for our mantissa, so we do multiple Pow with smaller powers.
                Pow(Pow(value, 2), (double)power / 2)
                : Normalize(mantissa, value.Exponent * power);
        }

        public static BigDouble Pow(BigDouble value, double power)
        {
            // TODO: power can be greater that long.MaxValue, which can bring troubles in fast track
            var powerIsInteger = IsInteger(power);
            if (value < 0 && !powerIsInteger)
            {
                return NaN;
            }

            return Is10(value) && powerIsInteger ? Pow10(power) : PowInternal(value, power);
        }

        private static bool Is10(BigDouble value)
        {
            return value.Exponent == 1 && value.Mantissa - 1 < double.Epsilon;
        }

        private static BigDouble PowInternal(BigDouble value, double other)
        {
            //UN-SAFETY: Accuracy not guaranteed beyond ~9~11 decimal places.

            //TODO: Fast track seems about neutral for performance. It might become faster if an integer pow is implemented, or it might not be worth doing (see https://github.com/Patashu/break_infinity.js/issues/4 )

            //Fast track: If (this.exponent*value) is an integer and mantissa^value fits in a Number, we can do a very fast method.
            var temp = value.Exponent * other;
            double newMantissa;
            if (IsInteger(temp) && IsFinite(temp) && math.abs(temp) < ExpLimit)
            {
                newMantissa = math.pow(value.Mantissa, other);
                if (IsFinite(newMantissa))
                {
                    return Normalize(newMantissa, (long)temp);
                }
            }

            //Same speed and usually more accurate. (An arbitrary-precision version of this calculation is used in break_break_infinity.js, sacrificing performance for utter accuracy.)

            var newexponent = math.trunc(temp);
            var residue = temp - newexponent;
            newMantissa = math.pow(10, other * math.log10(value.Mantissa) + residue);
            if (IsFinite(newMantissa))
            {
                return Normalize(newMantissa, (long)newexponent);
            }

            //UN-SAFETY: This should return NaN when mantissa is negative and value is noninteger.
            var result = Pow10(other * AbsLog10(value)); //this is 2x faster and gives same values AFAIK
            if (Sign(value) == -1 && AreEqual(other % 2, 1))
            {
                return -result;
            }

            return result;
        }

        public static BigDouble Factorial(BigDouble value)
        {
            //Using Stirling's Approximation. https://en.wikipedia.org/wiki/Stirling%27s_approximation#Versions_suitable_for_calculators

            var n = value.ToDouble() + 1;

            return Pow(n / 2.71828182845904523536 * math.sqrt(n * math.sinh(1 / n) + 1 / (810 * math.pow(n, 6))), n) * math.sqrt(2 * 3.141592653589793238462 / n);
        }

        public static BigDouble Exp(BigDouble value)
        {
            return Pow(2.71828182845904523536, value);
        }

        public static BigDouble Sqrt(BigDouble value)
        {
            if (value.Mantissa < 0)
            {
                return new BigDouble(double.NaN);
            }

            return value.Exponent % 2 != 0
                ?
                // mod of a negative number is negative, so != means '1 or -1'
                Normalize(math.sqrt(value.Mantissa) * 3.16227766016838, (long)math.floor(value.Exponent / 2.0))
                : Normalize(math.sqrt(value.Mantissa), (long)math.floor(value.Exponent / 2.0));
        }

        public static BigDouble Cbrt(BigDouble value)
        {
            var sign = 1;
            var mantissa = value.Mantissa;
            if (mantissa < 0)
            {
                sign = -1;
                mantissa = -mantissa;
            }

            var newmantissa = sign * math.pow(mantissa, 1 / 3.0);

            var mod = value.Exponent % 3;
            if (mod == 1 || mod == -1)
            {
                return Normalize(newmantissa * 2.1544346900318837, (long)math.floor(value.Exponent / 3.0));
            }

            return mod != 0
                ? Normalize(newmantissa * 4.6415888336127789, (long)math.floor(value.Exponent / 3.0))
                :
                //mod != 0 at this point means 'mod == 2 || mod == -2'
                Normalize(newmantissa, (long)math.floor(value.Exponent / 3.0));
        }

        public static BigDouble Sinh(BigDouble value)
        {
            return (Exp(value) - Exp(-value)) / 2;
        }

        public static BigDouble Cosh(BigDouble value)
        {
            return (Exp(value) + Exp(-value)) / 2;
        }

        public static BigDouble Tanh(BigDouble value)
        {
            return Sinh(value) / Cosh(value);
        }

        public static double Asinh(BigDouble value)
        {
            return Ln(value + Sqrt(Pow(value, 2) + 1));
        }

        public static double Acosh(BigDouble value)
        {
            return Ln(value + Sqrt(Pow(value, 2) - 1));
        }

        public static double Atanh(BigDouble value)
        {
            if (Abs(value) >= 1) return double.NaN;
            return Ln((value + 1) / (One - value)) / 2;
        }

        internal static bool IsZero(double value)
        {
            return math.abs(value) < double.Epsilon;
        }

        private static bool AreEqual(double first, double second)
        {
            return math.abs(first - second) < Tolerance;
        }

        private static bool IsInteger(double value)
        {
            return IsZero(math.abs(value % 1));
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }


        /// <summary>
        /// We need this lookup table because math.pow(10, exponent) when exponent's absolute value
        /// is large is slightly inaccurate. you can fix it with the power of math... or just make
        /// a lookup table. Faster AND simpler.
        /// </summary>
        public struct PowersOf10
        {
            private static readonly SharedStatic<NativeArray<double>> Powers = SharedStatic<NativeArray<double>>.GetOrCreate<PowersOf10, PowersFieldKey>();

            private class PowersFieldKey
            {
            }

            private const long IndexOf0 = -DoubleExpMin - 1;

            [BurstDiscard]
            public void Init()
            {
                Powers.Data = new NativeArray<double>((int)(DoubleExpMax - DoubleExpMin), Allocator.Persistent);
                var index = 0;
                for (var i = 0; i < Powers.Data.Length; i++)
                {
                    Powers.Data[index++] = double.Parse("1e" + (i - IndexOf0), CultureInfo.InvariantCulture);
                }
            }

            public static double Lookup(long power)
            {
                return Powers.Data[(int)(IndexOf0 + power)];
            }

            public void Dispose()
            {
                Powers.Data.Dispose();
            }
        }
    }
}