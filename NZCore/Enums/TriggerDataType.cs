// <copyright project="NZCore" file="TriggerDataType.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore
{
    [Serializable]
    public enum TriggerDataType : byte
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

        // 8 bits
        Byte,

        // 1 bit
        Bool, // todo bool counts as byte
    }

    [Serializable]
    public enum TriggerDataUsage : byte
    {
        Generic,
        CombatEventIndex
    }
}