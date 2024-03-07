using Unity.Entities;

namespace NZCore
{
    public enum ChildDestroyMethod : byte
    {
        None,
        Destroy,
        Cleanup
    }
    
    public struct DestroyEntity : IComponentData, IEnableableComponent
    {
    }
    
    public struct NZCleanupEntity : IComponentData, IEnableableComponent
    {
    }

    public struct DestroyMapElement
    {
        public Entity Entity;
        public ChildDestroyMethod DestroyMethod;
        public byte Payload;
    }

    public struct DeferredDestroyMapBuffer : IBufferElementData
    {
        public Entity Parent;
        public Entity Child;
        public ChildDestroyMethod DestroyMethod;
        public byte Payload;
    }
}