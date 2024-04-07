using NZSpellCasting;
using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(SavingSystemGroup))]
    public partial class InstantiateSystemGroup : ComponentSystemGroup
    {
    }
    
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SavingSystemGroup))]
    public partial class PostInstantiateSystemGroup : ComponentSystemGroup
    {
    }
}