// <copyright project="NZCore" file="GenericDataType.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore
{
    [Serializable]
    public enum GenericDataType
    {
        None,

        // 16 bits
        Short,
        Half,

        // 32 bits
        Float,
        Int,
        UInt,

        // 64 bits
        Double,
        Long,
        ULong,

        // 8 bits
        Byte,

        // 1 bit
        Bool // bool counts as byte
    }
}