// <copyright project="NZCore" file="BigNumber.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Globalization;

namespace NZCore
{
    public static class BigNumber
    {
        public static string FormatBigDouble(BigDouble value, string format, IFormatProvider formatProvider)
        {
            if (BigDouble.IsNaN(value)) return "NaN";
            if (value.Exponent >= BigDouble.ExpLimit)
            {
                return value.Mantissa > 0 ? "Infinity" : "-Infinity";
            }

            var formatSpecifier = ParseFormatSpecifier(format, out var formatDigits);
            switch (formatSpecifier)
            {
                case 'R':
                case 'G':
                    return FormatGeneral(value, formatDigits);
                case 'E':
                    return FormatExponential(value, formatDigits);
                case 'F':
                    return FormatFixed(value, formatDigits);
            }

            throw new FormatException($"Unknown string format '{formatSpecifier}'");
        }

        private static char ParseFormatSpecifier(string format, out int digits)
        {
            const char customFormat = (char)0;
            digits = -1;
            if (string.IsNullOrEmpty(format))
            {
                return 'R';
            }

            var i = 0;
            var ch = format[i];
            if ((ch < 'A' || ch > 'Z') && (ch < 'a' || ch > 'z'))
            {
                return customFormat;
            }

            i++;
            var n = -1;

            if (i < format.Length && format[i] >= '0' && format[i] <= '9')
            {
                n = format[i++] - '0';
                while (i < format.Length && format[i] >= '0' && format[i] <= '9')
                {
                    n = n * 10 + (format[i++] - '0');
                    if (n >= 10)
                        break;
                }
            }

            if (i < format.Length && format[i] != '\0')
            {
                return customFormat;
            }

            digits = n;
            return ch;
        }

        private static string FormatGeneral(BigDouble value, int places)
        {
            if (value.Exponent <= -BigDouble.ExpLimit || BigDouble.IsZero(value.Mantissa))
            {
                return "0";
            }

            var format = places > 0 ? $"G{places}" : "G";
            if (value.Exponent < 21 && value.Exponent > -7)
            {
                return value.ToDouble().ToString(format, CultureInfo.InvariantCulture);
            }

            return value.Mantissa.ToString(format, CultureInfo.InvariantCulture)
                   + "E" + (value.Exponent >= 0 ? "+" : "")
                   + value.Exponent.ToString(CultureInfo.InvariantCulture);
        }

        private static string ToFixed(double value, int places)
        {
            return value.ToString($"F{places}", CultureInfo.InvariantCulture);
        }

        private static string FormatExponential(BigDouble value, int places)
        {
            if (value.Exponent <= -BigDouble.ExpLimit || BigDouble.IsZero(value.Mantissa))
            {
                return "0" + (places > 0 ? ".".PadRight(places + 1, '0') : "") + "E+0";
            }

            var len = (places >= 0 ? places : BigDouble.MaxSignificantDigits) + 1;
            var numDigits = (int)Math.Ceiling(Math.Log10(Math.Abs(value.Mantissa)));
            var rounded = Math.Round(value.Mantissa * Math.Pow(10, len - numDigits)) * Math.Pow(10, numDigits - len);

            var mantissa = ToFixed(rounded, Math.Max(len - numDigits, 0));
            if (mantissa != "0" && places < 0)
            {
                mantissa = mantissa.TrimEnd('0', '.');
            }

            return mantissa + "E" + (value.Exponent >= 0 ? "+" : "")
                   + value.Exponent;
        }

        private static string FormatFixed(BigDouble value, int places)
        {
            if (places < 0)
            {
                places = BigDouble.MaxSignificantDigits;
            }

            if (value.Exponent <= -BigDouble.ExpLimit || BigDouble.IsZero(value.Mantissa))
            {
                return "0" + (places > 0 ? ".".PadRight(places + 1, '0') : "");
            }

            // two cases:
            // 1) exponent is 17 or greater: just print out mantissa with the appropriate number of zeroes after it
            // 2) exponent is 16 or less: use basic toFixed

            if (value.Exponent >= BigDouble.MaxSignificantDigits)
            {
                // TODO: StringBuilder-optimizable
                return value.Mantissa
                           .ToString(CultureInfo.InvariantCulture)
                           .Replace(".", "")
                           .PadRight((int)value.Exponent + 1, '0')
                       + (places > 0 ? ".".PadRight(places + 1, '0') : "");
            }

            return ToFixed(value.ToDouble(), places);
        }
    }
}