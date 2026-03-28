// <copyright project="NZCore.MVVM" file="IViewModelBindingNotify.cs" version="1.0.0">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.UIElements;

namespace NZCore.UI
{
    public interface IViewModelBindingNotify : INotifyBindablePropertyChanged
    {
        //void OnPropertyChanging(in FixedString64Bytes property);
        void OnPropertyChanged(in FixedString64Bytes property);
    }

    public interface IViewModelBindingNotify<T> : IViewModelBindingNotify
        where T : unmanaged
    {
        ref T Value { get; }

        public static unsafe void Load(IViewModelBindingNotify<T> viewModelBindingNotify)
        {
            var ptr = (IntPtr)UnsafeUtility.AddressOf(ref viewModelBindingNotify.Value);
            BurstUIInterop.Objects[ptr] = viewModelBindingNotify;
        }

        public static unsafe void Unload(IViewModelBindingNotify<T> viewModelBindingNotify)
        {
            var ptr = (IntPtr)UnsafeUtility.AddressOf(ref viewModelBindingNotify.Value);
            BurstUIInterop.Objects.Remove(ptr);
        }
    }

    public static class ViewModelBindingExtensions
    {
        public static void Load<T>(this IViewModelBindingNotify<T> bindingObjectNotify)
            where T : unmanaged
        {
            IViewModelBindingNotify<T>.Load(bindingObjectNotify);
        }

        public static void Unload<T>(this IViewModelBindingNotify<T> bindingObjectNotify)
            where T : unmanaged
        {
            IViewModelBindingNotify<T>.Unload(bindingObjectNotify);
        }
    }
}
#endif