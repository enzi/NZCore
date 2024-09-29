// <copyright project="NZCore" file="GenericMathProcessor.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Mathematics;
using UnityEngine;

namespace NZCore
{
    public static partial class GenericMath
    {
        public static bool ProcessValuesReturnChange<T, TProcessor>(ref T leftValue, T rightValue, MathOperator mathOperator)
            where T : unmanaged, IEquatable<T>
            where TProcessor : struct, IGenericValueCalculator<T>
        {
            var processor = default(TProcessor);

            switch (mathOperator)
            {
                case MathOperator.Set:
                {
                    var newValue = rightValue;
                    if (!newValue.Equals(leftValue))
                    {
                        Debug.Log($"SET {leftValue} = {newValue}");
                        return true;
                    }

                    break;
                }
                case MathOperator.Add:
                {
                    var newValue = processor.Add(leftValue, rightValue);
                    if (!newValue.Equals(leftValue))
                    {
                        leftValue = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.Subtract:
                {
                    var newValue = processor.Subtract(leftValue, rightValue);
                    if (!newValue.Equals(leftValue))
                    {
                        leftValue = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.Multiply:
                {
                    var newValue = processor.Multiply(leftValue, rightValue);
                    if (!newValue.Equals(leftValue))
                    {
                        leftValue = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.Divide:
                {
                    var newValue = processor.Divide(leftValue, rightValue);
                    if (!newValue.Equals(leftValue))
                    {
                        leftValue = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.PowerAtoB:
                {
                    var newValue = processor.PowerAtoB(leftValue, rightValue);
                    if (!newValue.Equals(leftValue))
                    {
                        leftValue = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.PowerBtoA:
                {
                    var newValue = processor.PowerBtoA(leftValue, rightValue);
                    if (!newValue.Equals(leftValue))
                    {
                        leftValue = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.Min:
                {
                    var newValue = processor.Min(leftValue, rightValue);
                    if (!newValue.Equals(leftValue))
                    {
                        leftValue = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.Max:
                {
                    var newValue = processor.Max(leftValue, rightValue);
                    if (!newValue.Equals(leftValue))
                    {
                        leftValue = newValue;
                        return true;
                    }

                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return false;
        }
        
        public static unsafe bool ProcessValues<T, TProcessor>(byte* valuePtr, MathOperator mathOperator, T rightValue)
            where T : unmanaged, IEquatable<T>
            where TProcessor : struct, IGenericValueCalculator<T>
        {
            var processor = default(TProcessor);
            ref var currentValue = ref *(T*)valuePtr;

            switch (mathOperator)
            {
                case MathOperator.Set:
                {
                    var newValue = rightValue;
                    if (!newValue.Equals(currentValue))
                    {
                        Debug.Log($"SET {currentValue} = {newValue}");
                        currentValue = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.Add:
                {
                    var newValue = processor.Add(currentValue, rightValue);
                    if (!newValue.Equals(currentValue))
                    {
                        currentValue = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.Subtract:
                {
                    var newValue = processor.Subtract(currentValue, rightValue);
                    if (!newValue.Equals(currentValue))
                    {
                        currentValue = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.Multiply:
                {
                    var newValue = processor.Multiply(currentValue, rightValue);
                    if (!newValue.Equals(currentValue))
                    {
                        currentValue = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.Divide:
                {
                    var newValue = processor.Divide(currentValue, rightValue);
                    if (!newValue.Equals(currentValue))
                    {
                        currentValue = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.PowerAtoB:
                {
                    var newValue = processor.PowerAtoB(currentValue, rightValue);
                    if (!newValue.Equals(currentValue))
                    {
                        currentValue = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.PowerBtoA:
                {
                    var newValue = processor.PowerBtoA(currentValue, rightValue);
                    if (!newValue.Equals(currentValue))
                    {
                        currentValue = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.Min:
                {
                    var newValue = processor.Min(currentValue, rightValue);
                    if (!newValue.Equals(currentValue))
                    {
                        currentValue = newValue;
                        return true;
                    }

                    break;
                }
                case MathOperator.Max:
                {
                    var newValue = processor.Max(currentValue, rightValue);
                    if (!newValue.Equals(currentValue))
                    {
                        currentValue = newValue;
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
            ref T valueA = ref *(T*)valuePtrToA;
            valueA = ProcessValues<T, TProcessor>(valueA, valuePtrToB, mathOperator);
        }

        public static unsafe T ProcessValues<T, TProcessor>(byte* valuePtrToA, T valueB, MathOperator mathOperator)
            where T : unmanaged, IEquatable<T>
            where TProcessor : struct, IGenericValueCalculator<T>
        {
            return ProcessValues<T, TProcessor>(*(T*)valuePtrToA, valueB, mathOperator);
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

            return mathOperator switch
            {
                MathOperator.Set => valueB,
                MathOperator.Add => processor.Add(valueA, valueB),
                MathOperator.Subtract => processor.Subtract(valueA, valueB),
                MathOperator.Multiply => processor.Multiply(valueA, valueB),
                MathOperator.Divide => processor.Divide(valueA, valueB),
                MathOperator.PowerAtoB => processor.PowerAtoB(valueA, valueB),
                MathOperator.PowerBtoA => processor.PowerBtoA(valueA, valueB),
                MathOperator.Min => processor.Min(valueA, valueB),
                MathOperator.Max => processor.Max(valueA, valueB),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        public static GenericUnionValue ProcessValues(TriggerDataType dataType, GenericUnionValue leftValue, GenericUnionValue rightValue, MathOperator mathOperator)
        {
            switch (dataType)
            {
                case TriggerDataType.Short:
                    return new GenericUnionValue() { ShortValue = GenericMath.ProcessValues(leftValue.ShortValue, rightValue.ShortValue, mathOperator) };
                case TriggerDataType.Half:
                    return new GenericUnionValue() { HalfValue = GenericMath.ProcessValues(leftValue.HalfValue, rightValue.HalfValue, mathOperator) };
                case TriggerDataType.Float:
                    return new GenericUnionValue() { FloatValue = GenericMath.ProcessValues(leftValue.FloatValue, rightValue.FloatValue, mathOperator) };
                case TriggerDataType.Int:
                    return new GenericUnionValue() { IntValue = GenericMath.ProcessValues(leftValue.IntValue, rightValue.IntValue, mathOperator) };
                case TriggerDataType.UInt:
                    return new GenericUnionValue() { UIntValue = GenericMath.ProcessValues(leftValue.UIntValue, rightValue.UIntValue, mathOperator) };
                case TriggerDataType.Double:
                    return new GenericUnionValue() { DoubleValue = GenericMath.ProcessValues(leftValue.DoubleValue, rightValue.DoubleValue, mathOperator) };
                case TriggerDataType.ULong:
                    return new GenericUnionValue() { ULongValue = GenericMath.ProcessValues(leftValue.ULongValue, rightValue.ULongValue, mathOperator) };
                case TriggerDataType.Long:
                    return new GenericUnionValue() { LongValue = GenericMath.ProcessValues(leftValue.LongValue, rightValue.LongValue, mathOperator) };
                case TriggerDataType.Byte:
                    return new GenericUnionValue() { ByteValue = GenericMath.ProcessValues(leftValue.ByteValue, rightValue.ByteValue, mathOperator) };
                case TriggerDataType.Bool:
                case TriggerDataType.None:
                default:
                    throw new ArgumentOutOfRangeException($"For dataType {dataType}");
            }
        }

        public static GenericUnionValue ProcessMathFunction(GenericDataType dataType, MathFunction mathFunction, GenericUnionValue value)
        {
            switch (dataType)
            {
                case GenericDataType.Short:
                    return new GenericUnionValue() { ShortValue = mathFunction.ProcessMathFunction(value.ShortValue) };
                case GenericDataType.Half:
                    return new GenericUnionValue() { HalfValue = mathFunction.ProcessMathFunction(value.HalfValue) };
                case GenericDataType.Float:
                    return new GenericUnionValue() { FloatValue = mathFunction.ProcessMathFunction(value.FloatValue) };
                case GenericDataType.Int:
                    return new GenericUnionValue() { IntValue = mathFunction.ProcessMathFunction(value.IntValue) };
                case GenericDataType.UInt:
                    return new GenericUnionValue() { UIntValue = mathFunction.ProcessMathFunction(value.UIntValue) };
                case GenericDataType.Double:
                    return new GenericUnionValue() { DoubleValue = mathFunction.ProcessMathFunction(value.DoubleValue) };
                case GenericDataType.ULong:
                    return new GenericUnionValue() { ULongValue = mathFunction.ProcessMathFunction(value.ULongValue) };
                case GenericDataType.Long:
                    return new GenericUnionValue() { LongValue = mathFunction.ProcessMathFunction(value.LongValue) };
                case GenericDataType.Byte:
                    return new GenericUnionValue() { ByteValue = mathFunction.ProcessMathFunction(value.ByteValue) };
                case GenericDataType.Bool:
                case GenericDataType.None:
                default:
                    throw new ArgumentOutOfRangeException($"For dataType {dataType}");
            }
        }

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