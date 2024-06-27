// <copyright file="IBindingObject.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

using UnityEngine.UIElements;

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