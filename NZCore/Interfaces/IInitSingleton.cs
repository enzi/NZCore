using Unity.Entities;

namespace NZCore.Interfaces
{
    public interface IInitSingleton : IComponentData
    {
        public void Init();
    }
}