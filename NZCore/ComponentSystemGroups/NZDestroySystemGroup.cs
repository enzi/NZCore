// <copyright project="NZCore" file="NZDestroySystemGroup.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
#if NZSPELLCASTING
using NZSpellCasting;
#endif

namespace NZCore
{
#if NZSPELLCASTING
    [UpdateInGroup(typeof(NZSpellCastingFixedInitializationSystemGroup))]
#else
    [UpdateInGroup(typeof(InitializationSystemGroup))]
#endif
    [UpdateBefore(typeof(NZDestroySystemGroup))]
    public partial class NZPreDestroySystemGroup : ComponentSystemGroup { }

#if NZSPELLCASTING
    [UpdateInGroup(typeof(NZSpellCastingFixedInitializationSystemGroup))]
    [UpdateAfter(typeof(BeginEffectsSystemGroupCommandBufferSystem))]
    [UpdateBefore(typeof(NZEffectsInitializationSystemGroup))]
#else
    [UpdateInGroup(typeof(InitializationSystemGroup))]
#endif
    public partial class NZDestroySystemGroup : ComponentSystemGroup { }
}