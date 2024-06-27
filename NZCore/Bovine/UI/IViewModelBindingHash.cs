// <copyright file="IBindingObjectHash.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if UNITY_6000
using UnityEngine.UIElements;

namespace BovineLabs.Core.UI
{
    public interface IViewModelBindingHash<T> : IViewModelBinding<T>, IDataSourceViewHashProvider
        where T : unmanaged, IModelBindingHashData
    {
        long IDataSourceViewHashProvider.GetViewHashCode()
        {
            return Value.Version;
        }
    }

    public interface IModelBindingHashData : IModelBinding
    {
        long Version { get; set; }
    }
}
#endif