// <copyright file="IBindingObject.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if UNITY_2023_3_0
namespace BovineLabs.Core.UI
{
    public interface IBindingObject
    {
    }

    public unsafe interface IBindingObject<T> : IBindingObject
        where T : unmanaged
    {
        ref T Value { get; }

        void Load()
        {
        }

        void Unload()
        {
        }
    }
    
    public unsafe interface IBindingPtrObject<T> : IBindingObject<T>
        where T : unmanaged
    {
        T* Ptr { set; }
    }
}
#endif