using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(NZSpellCastingSimulationSystemGroup))]
    public partial class NZSpellCastingFixedInitializationSystemGroup : ComponentSystemGroup
    {
    }
}