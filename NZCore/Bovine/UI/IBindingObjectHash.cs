// <copyright file="IBindingObjectHash.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if UNITY_2023_3_0
using UnityEngine.UIElements;

namespace BovineLabs.Core.UI
{
    public interface IBindingObjectHash<T> : IBindingObject<T>, IDataSourceViewHashProvider
        where T : unmanaged, IBindingObjectHashData
    {
        long IDataSourceViewHashProvider.GetViewHashCode()
        {
            return this.Value.Version;
        }
    }

    public interface IBindingObjectHashData
    {
        long Version { get; set; }
    }
}
#endif