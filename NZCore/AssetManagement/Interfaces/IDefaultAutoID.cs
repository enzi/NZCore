using System;

namespace NZCore.AssetManagement.Interfaces
{
    public interface IDefaultAutoID
    {
        public bool Default { get; }

        public Type DefaultType { get; }
    }
}