// <copyright project="NZCore" file="ConditionLogicValueComparison.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

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