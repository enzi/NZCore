using System.Runtime.CompilerServices;

namespace NZCore.Helper
{
    public static class LogicalComparisonHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool LogicalComparison(this ConditionLogicValueComparison logicValueComparison, double leftValue, double rightValue)
        {
            switch (logicValueComparison)
            {
                case ConditionLogicValueComparison.Any:
                    return leftValue > 0;
                case ConditionLogicValueComparison.Equal:
                    return leftValue == rightValue;
                case ConditionLogicValueComparison.NotEqual:
                    return leftValue != rightValue;
                case ConditionLogicValueComparison.GreaterThan:
                    return leftValue > rightValue;
                case ConditionLogicValueComparison.LesserThan:
                    return leftValue < rightValue;
                case ConditionLogicValueComparison.GreaterEqual:
                    return leftValue >= rightValue;
                case ConditionLogicValueComparison.LesserEqual:
                    return leftValue <= rightValue;
                case ConditionLogicValueComparison.HasFlag:
                    return ((int) leftValue & (int) rightValue) == (int) rightValue;
            }

            return false;
        }
    }
}