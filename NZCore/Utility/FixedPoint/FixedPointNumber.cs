// <copyright project="NZCore" file="FixedPointNumber.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore
{
    // public interface IFixedPointNumber<T>
    //     where T : unmanaged
    // {
    //     //static const T ValuesPerOne = T(1) << TPrecision;
    //     public const int Precision = 0;
    //     
    //     public T ValuesPerOne => (T) 1 << Precision;
    //
    // }
    //
    // public struct FixedPointNumberInt : IFixedPointNumber<int>
    // {
    //     override
    // }
    public interface IFixedPoint<T>
        where T : unmanaged
    {
        public double GetDouble();
        public float GetFloat();

        public static T GetOriginal(double val)
        {
            return default;
        }
    }

    public struct FixedPointInt24 : IFixedPoint<FixedPointInt24>
    {
        private int _value;
        
        public const int Precision = 24;

        public FixedPointInt24(int value)
        {
            _value = value << Precision;
        }

        public double GetDouble()
        {
            return _value * 5.960464477539062e-08;
        }

        public float GetFloat()
        {
            //throw new NotImplementedException();
            return 0;
        }

        public static FixedPointInt24 Unnormalize(double val)
        {
            var tmp = (int)(val / 5.960464477539062e-08);
            return new FixedPointInt24()
            {
                _value = tmp
            };
        }
    }
    
    public static class FixedPointInt24Extensions
    {
        public static void Multiply(this ref FixedPointInt24 val2, double normalizedFixedPoint)
        {
            var tmp = val2.GetDouble() * normalizedFixedPoint;
            val2 = new FixedPointInt24((int) tmp);
        }
    }
    
    public struct FixedPointInt8 : IFixedPoint<FixedPointInt24>
    {
        private int _value;
        
        public const int Precision = 8;

        public FixedPointInt8(int value)
        {
            _value = value << Precision;
        }

        public double GetDouble()
        {
            return _value * 0.00390625;
        }

        public float GetFloat()
        {
            //throw new NotImplementedException();
            return 0;
        }

        public static FixedPointInt8 Unnormalize(double val)
        {
            var tmp = (int)(val / 0.00390625);
            return new FixedPointInt8()
            {
                _value = tmp
            };
        }
    }
    
    public static class FixedPointInt8Extensions
    {
        public static void Multiply(this ref FixedPointInt8 val2, double normalizedFixedPoint)
        {
            var tmp = val2.GetDouble() * normalizedFixedPoint;
            val2 = new FixedPointInt8((int) tmp);
        }
    }
    
    
    public struct FixedPointFloat24 : IFixedPoint<FixedPointFloat24>
    {
        public float Value;
        
        public const int Precision = 24;

        public double GetDouble()
        {
            return Value;
        }

        public float GetFloat()
        {
            return Value;
        }

        public static FixedPointFloat24 Unnormalize(double val)
        {
            return new FixedPointFloat24()
            {
                Value = (float) val
            };
        }
    }
    
    public static class FixedPointFloat24Extensions
    {
        public static void Multiply(this ref FixedPointFloat24 val2, double normalizedFixedPoint)
        {
            var tmp = val2.GetDouble() * normalizedFixedPoint;
            val2 = FixedPointFloat24.Unnormalize(tmp);
        }
    }
    
    public struct FixedPointLong24 : IFixedPoint<FixedPointLong24>
    {
        private long Value;
        
        public const int Precision = 24;
        
        public FixedPointLong24(int value)
        {
            Value = value << Precision;
        }

        public double GetDouble()
        {
            return Value * 5.960464477539062e-08;
        }

        public float GetFloat()
        {
            return Value;
        }

        public static FixedPointLong24 Unnormalize(double val)
        {
            var tmp = (long)(val / 5.960464477539062e-08);
            return new FixedPointLong24()
            {
                Value = tmp
            };
        }
    }
    
    public static class FixedPointLong24Extensions
    {
        public static void Multiply(this ref FixedPointLong24 val2, double normalizedFixedPoint)
        {
            var tmp = val2.GetDouble() * normalizedFixedPoint;
            val2 = FixedPointLong24.Unnormalize(tmp);
        }
    }
}