// <copyright project="NZCore" file="StatDataType.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore
{
    [Serializable]
    public enum StatDataType : byte
    {
        None,

        // 16 bits
        Half,
        Short,
        UShort,

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
        Bool
    }
}