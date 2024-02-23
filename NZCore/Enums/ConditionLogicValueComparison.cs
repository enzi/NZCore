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
        LesserEqual
    }
    
    [Serializable]
    public enum ConditionLogicalConnection : byte
    {
        AND,
        OR
    }
}