// <copyright file="IBindingObjectNotify.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using UnityEngine.UIElements;

namespace BovineLabs.Core.UI
{
    public delegate void OnPropertyChangedDelegate(in FixedString64Bytes property);

    public static class BindingObjectCollection
    {
        public static readonly Dictionary<IBindingObject, GCHandle> Handles = new();
    }

    public interface IBindingObjectNotify<T> : IBindingObject<T>, INotifyBindablePropertyChanged
        where T : unmanaged, IBindingObjectNotifyData
    {
        public static void Load(IBindingObjectNotify<T> bindingObjectNotify)
        {
            var propertyChanged = new OnPropertyChangedDelegate(bindingObjectNotify.OnPropertyChanged);
            BindingObjectCollection.Handles[bindingObjectNotify] = GCHandle.Alloc(propertyChanged, GCHandleType.Pinned);
            bindingObjectNotify.Value.Notify = new FunctionPointer<OnPropertyChangedDelegate>(Marshal.GetFunctionPointerForDelegate(propertyChanged));
        }

        public static void Unload(IBindingObjectNotify<T> bindingObjectNotify)
        {
            if (BindingObjectCollection.Handles.Remove(bindingObjectNotify, out var handle))
            {
                handle.Free();
            }

            bindingObjectNotify.Value.Notify = default;
        }

        /// <inheritdoc/>
        void IBindingObject<T>.Load()
        {
            Load(this);
        }

        /// <inheritdoc/>
        void IBindingObject<T>.Unload()
        {
            Unload(this);
        }

        void OnPropertyChanged(in FixedString64Bytes property);
    }
    
    public interface IBindingObjectPtrNotify<T> : IBindingPtrObject<T>, INotifyBindablePropertyChanged
        where T : unmanaged, IBindingObjectNotifyData
    {
        public static void Load(IBindingObjectPtrNotify<T> bindingObjectNotify)
        {
            var propertyChanged = new OnPropertyChangedDelegate(bindingObjectNotify.OnPropertyChanged);
            BindingObjectCollection.Handles[bindingObjectNotify] = GCHandle.Alloc(propertyChanged, GCHandleType.Pinned);
            bindingObjectNotify.Value.Notify = new FunctionPointer<OnPropertyChangedDelegate>(Marshal.GetFunctionPointerForDelegate(propertyChanged));
        }

        public static void Unload(IBindingObjectPtrNotify<T> bindingObjectNotify)
        {
            if (BindingObjectCollection.Handles.Remove(bindingObjectNotify, out var handle))
            {
                handle.Free();
            }

            bindingObjectNotify.Value.Notify = default;
        }

        /// <inheritdoc/>
        void IBindingObject<T>.Load()
        {
            Load(this);
        }

        /// <inheritdoc/>
        void IBindingObject<T>.Unload()
        {
            Unload(this);
        }

        void OnPropertyChanged(in FixedString64Bytes property);
    }

    public interface IBindingObjectNotifyData
    {
        FunctionPointer<OnPropertyChangedDelegate> Notify { get; set; }
    }

    public static class BindingObjectNotifyDataExtensions
    {
        public static void Notify<T>(this ref T binding, [CallerMemberName] string property = "")
            where T : unmanaged, IBindingObjectNotifyData
        {
            if (binding.Notify.IsCreated)
            {
                binding.Notify.Invoke(property);
            }
        }
    }
}
