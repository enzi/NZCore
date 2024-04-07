// <copyright file="IBindingObject.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if UNITY_6000
namespace BovineLabs.Core.UI
{
    public interface IBindingObject
    {
        void Load()
        {
        }

        void Unload()
        {
        }
    }


    public unsafe interface IBindingObject<T> : IBindingObject
        where T : unmanaged
    {
        ref T Value { get; }
    }
    
    public unsafe interface IBindingPtrObject<T> : IBindingObject<T>
        where T : unmanaged
    {
        T* Ptr { set; }
    }
}
#endif