using System;

namespace NZCore
{
    public interface ISavable
    {
    }
    
    public interface ISavableObject : IDisposable
    {
        void Init();
    }
}