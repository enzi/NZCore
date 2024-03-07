using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class NZDestroySystemGroup : ComponentSystemGroup
    {
    }
}