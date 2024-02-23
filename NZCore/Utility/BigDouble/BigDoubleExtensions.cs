namespace NZCore
{
    public static class BigDoubleExtensions
    {
        public static BigDouble Abs(this BigDouble value)
        {
            return BigDouble.Abs(value);
        }

        public static BigDouble Negate(this BigDouble value)
        {
            return BigDouble.Negate(value);
        }

        public static int Sign(this BigDouble value)
        {
            return BigDouble.Sign(value);
        }

        public static BigDouble Round(this BigDouble value)
        {
            return BigDouble.Round(value);
        }

        public static BigDouble Floor(this BigDouble value)
        {
            return BigDouble.Floor(value);
        }

        public static BigDouble Ceiling(this BigDouble value)
        {
            return BigDouble.Ceiling(value);
        }

        public static BigDouble Truncate(this BigDouble value)
        {
            return BigDouble.Truncate(value);
        }

        public static BigDouble Add(this BigDouble value, BigDouble other)
        {
            return BigDouble.Add(value, other);
        }

        public static BigDouble Subtract(this BigDouble value, BigDouble other)
        {
            return BigDouble.Subtract(value, other);
        }

        public static BigDouble Multiply(this BigDouble value, BigDouble other)
        {
            return BigDouble.Multiply(value, other);
        }

        public static BigDouble Divide(this BigDouble value, BigDouble other)
        {
            return BigDouble.Divide(value, other);
        }

        public static BigDouble Reciprocate(this BigDouble value)
        {
            return BigDouble.Reciprocate(value);
        }

        public static BigDouble Max(this BigDouble value, BigDouble other)
        {
            return BigDouble.Max(value, other);
        }

        public static BigDouble Min(this BigDouble value, BigDouble other)
        {
            return BigDouble.Min(value, other);
        }

        public static double AbsLog10(this BigDouble value)
        {
            return BigDouble.AbsLog10(value);
        }

        public static double Log10(this BigDouble value)
        {
            return BigDouble.Log10(value);
        }

        public static double Log(BigDouble value, BigDouble @base)
        {
            return BigDouble.Log(value, @base);
        }

        public static double Log(this BigDouble value, double @base)
        {
            return BigDouble.Log(value, @base);
        }

        public static double Log2(this BigDouble value)
        {
            return BigDouble.Log2(value);
        }

        public static double Ln(this BigDouble value)
        {
            return BigDouble.Ln(value);
        }

        public static BigDouble Exp(this BigDouble value)
        {
            return BigDouble.Exp(value);
        }

        public static BigDouble Sinh(this BigDouble value)
        {
            return BigDouble.Sinh(value);
        }

        public static BigDouble Cosh(this BigDouble value)
        {
            return BigDouble.Cosh(value);
        }

        public static BigDouble Tanh(this BigDouble value)
        {
            return BigDouble.Tanh(value);
        }

        public static double Asinh(this BigDouble value)
        {
            return BigDouble.Asinh(value);
        }

        public static double Acosh(this BigDouble value)
        {
            return BigDouble.Acosh(value);
        }

        public static double Atanh(this BigDouble value)
        {
            return BigDouble.Atanh(value);
        }

        public static BigDouble Pow(this BigDouble value, BigDouble power)
        {
            return BigDouble.Pow(value, power);
        }

        public static BigDouble Pow(this BigDouble value, long power)
        {
            return BigDouble.Pow(value, power);
        }

        public static BigDouble Pow(this BigDouble value, double power)
        {
            return BigDouble.Pow(value, power);
        }

        public static BigDouble Factorial(this BigDouble value)
        {
            return BigDouble.Factorial(value);
        }

        public static BigDouble Sqrt(this BigDouble value)
        {
            return BigDouble.Sqrt(value);
        }

        public static BigDouble Cbrt(this BigDouble value)
        {
            return BigDouble.Cbrt(value);
        }

        public static BigDouble Sqr(this BigDouble value)
        {
            return BigDouble.Pow(value, 2);
        }
    }
}