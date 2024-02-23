using Unity.Entities;

namespace NZCore.Components
{
    public struct RemoveFromLinkedEntityGroupCleanupSetup : IComponentData
    {
        public Entity Parent;
    }
    
    public struct RemoveFromLinkedEntityGroupCleanup : ICleanupComponentData
    {
        public Entity Parent;
    }
}