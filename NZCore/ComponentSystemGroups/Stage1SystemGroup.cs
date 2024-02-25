using NZSpellCasting;
using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class NZSpellCastingSimulationSystemGroup : ComponentSystemGroup
    {
    }
    
    [UpdateInGroup(typeof(NZSpellCastingInitializationSystemGroup))]
    public partial class NZEffectsInitializationSystemGroup : ComponentSystemGroup
    {
    }
    
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class NZSpellCastingPresentationSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(NZSpellCastingSimulationSystemGroup))]
    public partial class Stage1SystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(NZSpellCastingSimulationSystemGroup))]
    [UpdateAfter(typeof(Stage1SystemGroup))]
    public partial class Stage2SystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(NZSpellCastingSimulationSystemGroup))]
    [UpdateAfter(typeof(Stage2SystemGroup))]
    public partial class NZEffectsSystemGroup : ComponentSystemGroup
    {
    }
    
    [UpdateInGroup(typeof(NZSpellCastingSimulationSystemGroup))]
    [UpdateAfter(typeof(NZEffectsSystemGroup))]
    public partial class NZStatsSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(NZSpellCastingSimulationSystemGroup))]
    [UpdateAfter(typeof(NZStatsSystemGroup))]
    public partial class Stage5SystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(NZSpellCastingSimulationSystemGroup))]
    [UpdateAfter(typeof(Stage5SystemGroup))]
    public partial class Stage6SystemGroup : ComponentSystemGroup
    {
    }
    
    // [UpdateInGroup(typeof(NZSpellCastingSimulationSystemGroup))]
    // [UpdateAfter(typeof(Stage6SystemGroup))]
    // public partial class FixedPresentationGroup : ComponentSystemGroup
    // {
    // }

    [UpdateInGroup(typeof(NZSpellCastingSimulationSystemGroup))]
    [UpdateAfter(typeof(Stage6SystemGroup))]
    public partial class NZClearSystemGroup : ComponentSystemGroup
    {
    }
    
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    public partial class NZClearPresentationSystemGroup : ComponentSystemGroup
    {
    }
}