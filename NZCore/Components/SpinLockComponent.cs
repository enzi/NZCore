// <copyright project="NZSpellCasting.SpellCaster.Data" file="SpinLockComponent.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore;
using Unity.Entities;

namespace NZSpellCasting
{
    public struct SpinLockComponent : IComponentData
    {
        public SpinLock SpinLock;
    }
}
