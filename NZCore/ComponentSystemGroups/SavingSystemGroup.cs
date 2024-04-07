using Unity.Entities;
using Unity.Scenes;

namespace NZSpellCasting
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SceneSystemGroup))]
    public partial class SavingSystemGroup : ComponentSystemGroup
    {
    }
}