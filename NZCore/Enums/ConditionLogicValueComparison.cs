using System;

namespace NZCore
{
    [Serializable]
    public enum ConditionLogicValueComparison : byte
    {
        Any,
        Equal,
        NotEqual,
        GreaterThan,
        LesserThan,
        GreaterEqual,
        LesserEqual,
        HasFlag
    }
    
    [Serializable]
    public enum ConditionLogicalConnection : byte
    {
        AND,
        OR
    }
}