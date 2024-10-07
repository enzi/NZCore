// <copyright project="NZCore" file="IViewModelBinding.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000
namespace BovineLabs.Core.UI
{
    public interface IViewModelBinding
    {
        void Load()
        {
        }

        void Unload()
        {
        }
    }

    public interface IViewModelBinding<T> : IViewModelBinding
        where T : IModelBinding
    {
        ref T Value { get; }
    }
}
#endif