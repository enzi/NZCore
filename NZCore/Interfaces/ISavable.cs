using System;

namespace NZCore
{
    public interface ISavableObject : IDisposable
    {
        void Init();
    }
}