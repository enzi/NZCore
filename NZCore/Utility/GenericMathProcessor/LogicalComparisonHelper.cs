// <copyright project="NZCore" file="LogicalComparisonHelper.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.Helper
{
    public static class LogicalComparisonHelper
    {
        public static bool LogicalComparison(this ConditionLogicValueComparison logicValueComparison, TriggerDataType dataType, GenericUnionValue leftValue, GenericUnionValue rightValue)
        {
            switch (dataType)
            {
                case TriggerDataType.Short:
                    return logicValueComparison.LogicalComparison(leftValue.ShortValue, rightValue.ShortValue);
                case TriggerDataType.Half:
                    return logicValueComparison.LogicalComparison(leftValue.HalfValue, rightValue.HalfValue);
                case TriggerDataType.Float:
                    return logicValueComparison.LogicalComparison(leftValue.FloatValue, rightValue.FloatValue);
                case TriggerDataType.Int:
                    return logicValueComparison.LogicalComparison(leftValue.IntValue, rightValue.IntValue);
                case TriggerDataType.UInt:
                    return logicValueComparison.LogicalComparison(leftValue.UIntValue, rightValue.UIntValue);
                case TriggerDataType.Double:
                    return logicValueComparison.LogicalComparison(leftValue.DoubleValue, rightValue.DoubleValue);
                case TriggerDataType.ULong:
                    return logicValueComparison.LogicalComparison(leftValue.ULongValue, rightValue.ULongValue);
                case TriggerDataType.Long:
                    return logicValueComparison.LogicalComparison(leftValue.LongValue, rightValue.LongValue);
                case TriggerDataType.Byte:
                    return logicValueComparison.LogicalComparison(leftValue.ByteValue, rightValue.ByteValue);
                case TriggerDataType.Bool:
                case TriggerDataType.None:
                default:
                    throw new ArgumentOutOfRangeException($"For Key {dataType}");
            }
        }


        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // public static bool LogicalComparison<T, TProcessor>(this ConditionLogicValueComparison logicValueComparison, T leftValue, T rightValue)
        //     where T : unmanaged, IEquatable<T>
        //     where TProcessor : struct, IGenericValueCalculator<T>
        // {
        //     TProcessor processor = default;
        //
        //     switch (logicValueComparison)
        //     {
        //         case ConditionLogicValueComparison.Any:
        //             return processor.Any(leftValue, rightValue);
        //         case ConditionLogicValueComparison.Equal:
        //             return processor.Equal(leftValue, rightValue);
        //         case ConditionLogicValueComparison.NotEqual:
        //             return processor.NotEqual(leftValue, rightValue);
        //         case ConditionLogicValueComparison.GreaterThan:
        //             return processor.GreaterThan(leftValue, rightValue);
        //         case ConditionLogicValueComparison.LesserThan:
        //             return processor.LesserThan(leftValue, rightValue);
        //         case ConditionLogicValueComparison.GreaterEqual:
        //             return processor.GreaterEqual(leftValue, rightValue);
        //         case ConditionLogicValueComparison.LesserEqual:
        //             return processor.LesserEqual(leftValue, rightValue);
        //         case ConditionLogicValueComparison.HasFlag:
        //             return processor.HasFlag(leftValue, rightValue);
        //     }
        //
        //     return false;
        // }
    }
}