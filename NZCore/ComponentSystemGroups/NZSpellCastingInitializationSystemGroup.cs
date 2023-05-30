using Unity.Entities;

namespace NZSpellCasting
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(NZSpellCastingSpawnSystemGroup))]
    public partial class NZSpellCastingInitializationSystemGroup : ComponentSystemGroup
    {
    }
}