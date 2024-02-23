using System;
using NZCore;
using Unity.Mathematics;
using UnityEngine;

namespace NZSpellCasting.Runtime.Helper
{
    public static class GenericMathProcessor
    {
        public static unsafe bool ProcessValues<T, TProcessor>(byte* valuePtr, MathOperator mathOperator, T changeValue)
            where T : unmanaged, IEquatable<T>
            where TProcessor : struct, IGenericValueCalculator<T>
        {
            var processor = default(TProcessor);
            ref var value = ref *(T*)valuePtr;
            
            switch (mathOperator)
            {
                case MathOperator.Set:
                {
                    var newValue = changeValue;
                    if (!newValue.Equals(value))
                    {
                        Debug.Log($"SET {value} = {newValue}");
                        value = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.Add:
                {
                    var newValue = processor.Add(value, changeValue);
                    if (!newValue.Equals(value))
                    {
                        value = newValue;
                        return true;
                    }
                    
                    break;
                }
                case MathOperator.Subtract:
                {
                    var newValue = processor.Subtract(value, changeValue);
                    if (!newValue.Equals(value))
                    {
                        value = newValue;
                        return true;
                    }
                    
                    break;
                }
                case MathOperator.Multiply:
                {
                    var newValue = processor.Multiply(value, changeValue);
                    if (!newValue.Equals(value))
                    {
                        value = newValue;
                        return true;
                    }
                    
                    break;
                }
                case MathOperator.Divide:
                {
                    var newValue = processor.Divide(value, changeValue);
                    if (!newValue.Equals(value))
                    {
                        value = newValue;
                        return true;
                    }
                    
                    break;
                }
                case MathOperator.PowerAtoB:
                {
                    var newValue = processor.PowerAtoB(value, changeValue);
                    if (!newValue.Equals(value))
                    {
                        value = newValue;
                        return true;
                    }
                    
                    break;
                }
                case MathOperator.PowerBtoA:
                {
                    var newValue = processor.PowerBtoA(value, changeValue);
                    if (!newValue.Equals(value))
                    {
                        value = newValue;
                        return true;
                    }

                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return false;
        }
        
        public static unsafe bool ProcessValuesWithMinMax<T, TProcessor>(byte* valuePtr, MathOperator mathOperator, T changeValue, T minValue, T maxValue)
            where T : unmanaged, IEquatable<T>
            where TProcessor : struct, IGenericValueCalculator<T>
        {
            var processor = default(TProcessor);
            ref var value = ref *(T*)valuePtr;
            
            switch (mathOperator)
            {
                case MathOperator.Set:
                {
                    var newValue = changeValue;
                    newValue = processor.Min(newValue, maxValue);
                    newValue = processor.Max(newValue, minValue);
                    
                    if (!newValue.Equals(value))
                    {
                        Debug.Log($"SET {value} = {newValue}");
                        value = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.Add:
                {
                    var newValue = processor.Add(value, changeValue);
                    newValue = processor.Min(newValue, maxValue);
                    newValue = processor.Max(newValue, minValue);
                    
                    if (!newValue.Equals(value))
                    {
                        value = newValue;
                        return true;
                    }
                    
                    break;
                }
                case MathOperator.Subtract:
                {
                    var newValue = processor.Subtract(value, changeValue);
                    newValue = processor.Min(newValue, maxValue);
                    newValue = processor.Max(newValue, minValue);
                    
                    if (!newValue.Equals(value))
                    {
                        value = newValue;
                        return true;
                    }
                    
                    break;
                }
                case MathOperator.Multiply:
                {
                    var newValue = processor.Multiply(value, changeValue);
                    newValue = processor.Min(newValue, maxValue);
                    newValue = processor.Max(newValue, minValue);
                    
                    if (!newValue.Equals(value))
                    {
                        value = newValue;
                        return true;
                    }
                    
                    break;
                }
                case MathOperator.Divide:
                {
                    var newValue = processor.Divide(value, changeValue);
                    newValue = processor.Min(newValue, maxValue);
                    newValue = processor.Max(newValue, minValue);
                    
                    if (!newValue.Equals(value))
                    {
                        value = newValue;
                        return true;
                    }
                    
                    break;
                }
                case MathOperator.PowerAtoB:
                {
                    var newValue = processor.PowerAtoB(value, changeValue);
                    newValue = processor.Min(newValue, maxValue);
                    newValue = processor.Max(newValue, minValue);
                    
                    if (!newValue.Equals(value))
                    {
                        value = newValue;
                        return true;
                    }
                    
                    break;
                }
                case MathOperator.PowerBtoA:
                {
                    var newValue = processor.PowerBtoA(value, changeValue);
                    newValue = processor.Min(newValue, maxValue);
                    newValue = processor.Max(newValue, minValue);
                    
                    if (!newValue.Equals(value))
                    {
                        value = newValue;
                        return true;
                    }

                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return false;
        }

        public static unsafe void ProcessValues<T, TProcessor>(byte* valuePtrToA, byte* valuePtrToB, MathOperator mathOperator)
            where T : unmanaged, IEquatable<T>
            where TProcessor : struct, IGenericValueCalculator<T>
        {
            var processor = default(TProcessor);
            ref var valueA = ref *(T*)valuePtrToA;
            var valueB = *(T*)valuePtrToB;
            
            switch (mathOperator)
            {
                case MathOperator.Set:
                {
                    valueA = valueB;
                    break;
                }
                case MathOperator.Add:
                {
                    valueA = processor.Add(valueA, valueB);
                    break;
                }
                case MathOperator.Subtract:
                {
                    valueA = processor.Subtract(valueA, valueB);
                    break;
                }
                case MathOperator.Multiply:
                {
                    valueA = processor.Multiply(valueA, valueB);
                    break;
                }
                case MathOperator.Divide:
                {
                    valueA = processor.Divide(valueA, valueB);
                    break;
                }
                case MathOperator.PowerAtoB:
                {
                    valueA = processor.PowerAtoB(valueA, valueB);
                    
                    break;
                }
                case MathOperator.PowerBtoA:
                {
                    valueA = processor.PowerBtoA(valueA, valueB);

                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public static unsafe T ProcessValues<T, TProcessor>(T valueA, byte* valuePtrToB, MathOperator mathOperator)
            where T : unmanaged, IEquatable<T>
            where TProcessor : struct, IGenericValueCalculator<T>
        {
            return ProcessValues<T, TProcessor>(valueA, *(T*)valuePtrToB, mathOperator);
        }
        
        public static T ProcessValues<T, TProcessor>(T valueA, T valueB, MathOperator mathOperator)
            where T : unmanaged, IEquatable<T>
            where TProcessor : struct, IGenericValueCalculator<T>
        {
            var processor = default(TProcessor);
            
            switch (mathOperator)
            {
                case MathOperator.Set:
                {
                    return valueB;
                }
                case MathOperator.Add:
                {
                    return processor.Add(valueA, valueB);
                }
                case MathOperator.Subtract:
                {
                    return processor.Subtract(valueA, valueB);
                }
                case MathOperator.Multiply:
                {
                    return processor.Multiply(valueA, valueB);
                }
                case MathOperator.Divide:
                {
                    return processor.Divide(valueA, valueB);
                }
                case MathOperator.PowerAtoB:
                {
                    return processor.PowerAtoB(valueA, valueB);
                }
                case MathOperator.PowerBtoA:
                {
                    return processor.PowerBtoA(valueA, valueB);
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public interface IGenericValueCalculator<T>
        {
            T Add(T a, T b);
            T Subtract(T a, T b);
            T Multiply(T a, T b);
            T Divide(T a, T b);
            T PowerAtoB(T a, T b);
            T PowerBtoA(T a, T b);
            T Min(T a, T b);
            T Max(T a, T b);
        }

        public struct ShortProcessor : IGenericValueCalculator<short>
        {
            public short Add(short a, short b)
            {
                return (short)(a + b);
            }

            public short Subtract(short a, short b)
            {
                return (short)(a - b);
            }

            public short Multiply(short a, short b)
            {
                return (short)(a * b);
            }

            public short Divide(short a, short b)
            {
                return (short)(a / b);
            }

            public short PowerAtoB(short a, short b)
            {
                return (short) math.pow(a, b);
            }

            public short PowerBtoA(short a, short b)
            {
                return (short) math.pow(b, a);
            }

            public short Min(short a, short b)
            {
                return (short) math.min(a, b);
            }

            public short Max(short a, short b)
            {
                return (short) math.max(a, b);
            }
        }
        
        public struct HalfProcessor : IGenericValueCalculator<half>
        {
            public half Add(half a, half b)
            {
                return (half)(a + b);
            }

            public half Subtract(half a, half b)
            {
                return (half)(a - b);
            }

            public half Multiply(half a, half b)
            {
                return (half)(a * b);
            }

            public half Divide(half a, half b)
            {
                return (half)(a / b);
            }
            
            public half PowerAtoB(half a, half b)
            {
                return (half) math.pow(a, b);
            }

            public half PowerBtoA(half a, half b)
            {
                return (half) math.pow(b, a);
            }
            
            public half Min(half a, half b)
            {
                return (half) math.min(a, b);
            }

            public half Max(half a, half b)
            {
                return (half) math.max(a, b);
            }
        }
        
        public struct FloatProcessor : IGenericValueCalculator<float>
        {
            public float Add(float a, float b)
            {
                return a + b;
            }

            public float Subtract(float a, float b)
            {
                return a - b;
            }

            public float Multiply(float a, float b)
            {
                return a * b;
            }

            public float Divide(float a, float b)
            {
                return a / b;
            }
            
            public float PowerAtoB(float a, float b)
            {
                return math.pow(a, b);
            }

            public float PowerBtoA(float a, float b)
            {
                return math.pow(b, a);
            }
            
            public float Min(float a, float b)
            {
                return math.min(a, b);
            }

            public float Max(float a, float b)
            {
                return math.max(a, b);
            }
        }
        
        public struct IntProcessor : IGenericValueCalculator<int>
        {
            public int Add(int a, int b)
            {
                return a + b;
            }

            public int Subtract(int a, int b)
            {
                return a - b;
            }

            public int Multiply(int a, int b)
            {
                return a * b;
            }

            public int Divide(int a, int b)
            {
                return a / b;
            }
            
            public int PowerAtoB(int a, int b)
            {
                return (int) math.pow(a, b);
            }

            public int PowerBtoA(int a, int b)
            {
                return (int)math.pow(b, a);
            }
            
            public int Min(int a, int b)
            {
                return math.min(a, b);
            }

            public int Max(int a, int b)
            {
                return math.max(a, b);
            }
        }
        
        public struct UIntProcessor : IGenericValueCalculator<uint>
        {
            public uint Add(uint a, uint b)
            {
                return a + b;
            }

            public uint Subtract(uint a, uint b)
            {
                return a - b;
            }

            public uint Multiply(uint a, uint b)
            {
                return a * b;
            }

            public uint Divide(uint a, uint b)
            {
                return a / b;
            }
            
            public uint PowerAtoB(uint a, uint b)
            {
                return (uint) math.pow(a, b);
            }

            public uint PowerBtoA(uint a, uint b)
            {
                return (uint) math.pow(b, a);
            }
            
            public uint Min(uint a, uint b)
            {
                return math.min(a, b);
            }

            public uint Max(uint a, uint b)
            {
                return math.max(a, b);
            }
        }
        
        public struct DoubleProcessor : IGenericValueCalculator<double>
        {
            public double Add(double a, double b)
            {
                return a + b;
            }

            public double Subtract(double a, double b)
            {
                return a - b;
            }

            public double Multiply(double a, double b)
            {
                return a * b;
            }

            public double Divide(double a, double b)
            {
                return a / b;
            }
            
            public double PowerAtoB(double a, double b)
            {
                return (double) math.pow(a, b);
            }

            public double PowerBtoA(double a, double b)
            {
                return (double) math.pow(b, a);
            }
            
            public double Min(double a, double b)
            {
                return math.min(a, b);
            }

            public double Max(double a, double b)
            {
                return math.max(a, b);
            }
        }
        
        public struct ByteProcessor : IGenericValueCalculator<byte>
        {
            public byte Add(byte a, byte b)
            {
                return (byte) (a + b);
            }

            public byte Subtract(byte a, byte b)
            {
                return (byte) (a - b);
            }

            public byte Multiply(byte a, byte b)
            {
                return (byte) (a * b);
            }

            public byte Divide(byte a, byte b)
            {
                return (byte) (a / b);
            }
            
            public byte PowerAtoB(byte a, byte b)
            {
                return (byte) math.pow(a, b);
            }

            public byte PowerBtoA(byte a, byte b)
            {
                return (byte) math.pow(b, a);
            }
            
            public byte Min(byte a, byte b)
            {
                return (byte) math.min((int) a, (int) b);
            }

            public byte Max(byte a, byte b)
            {
                return (byte) math.max((int) a, (int) b);
            }
        }
        
        // public struct NZValueProcessor : IGenericValueCalculator<NZValue>
        // {
        //     public NZValue Add(NZValue a, NZValue b)
        //     {
        //         return (a + b);
        //     }
        //
        //     public NZValue Subtract(NZValue a, NZValue b)
        //     {
        //         return (a - b);
        //     }
        //
        //     public NZValue Multiply(NZValue a, NZValue b)
        //     {
        //         return (a * b);
        //     }
        //
        //     public NZValue Divide(NZValue a, NZValue b)
        //     {
        //         return (a / b);
        //     }
        //     
        //     public NZValue PowerAtoB(NZValue a, NZValue b)
        //     {
        //         return new NZValue() { Value = math.pow(a.Value, b.Value) };
        //     }
        //
        //     public NZValue PowerBtoA(NZValue a, NZValue b)
        //     {
        //         return new NZValue() { Value = math.pow(b.Value, a.Value) };
        //     }
        //     
        //     public NZValue Min(NZValue a, NZValue b)
        //     {
        //         return new NZValue() { Value = math.min(a.Value, b.Value) };
        //     }
        //
        //     public NZValue Max(NZValue a, NZValue b)
        //     {
        //         return new NZValue() { Value = math.max(a.Value, b.Value) };
        //     }
        // }

        // TODO wait until .NET6 arrives in Unity :)
        // public interface IEqualityOperators<TSelf, TOther, TResult> where TSelf : IEqualityOperators<TSelf, TOther, TResult>?
        // {
        //     static abstract TResult operator ==(TSelf? left, TOther? right);
        //     static abstract TResult operator !=(TSelf? left, TOther? right);
        // }
        //
        // public interface IComparisonOperators<TSelf, TOther, TResult> : IEqualityOperators<TSelf, TOther, TResult> where TSelf : IComparisonOperators<TSelf, TOther, TResult>?
        // {
        //     static abstract TResult operator <(TSelf left, TOther right);
        //     static abstract TResult operator >(TSelf left, TOther right);
        //     static abstract TResult operator <=(TSelf left, TOther right);
        //     static abstract TResult operator >=(TSelf left, TOther right);
        // }

        // public interface INumber<TSelf>
        //     : IComparable,
        //         IComparable<TSelf>,
        //         IComparisonOperators<TSelf, TSelf, bool>,
        //         IModulusOperators<TSelf, TSelf, TSelf>,
        //         INumberBase<TSelf>
        //     where TSelf : INumber<TSelf>?
        // {
        //     
        // }
    }
}