using Unity.Entities;

namespace NZSpellCasting
{
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    public partial class NZSpellCastingSpawnSystemGroup : ComponentSystemGroup
    {
    }
}