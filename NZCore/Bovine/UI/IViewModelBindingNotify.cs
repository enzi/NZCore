// <copyright project="NZCore" file="IViewModelBindingNotify.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.UIElements;

namespace BovineLabs.Core.UI
{
    public delegate void OnPropertyChangedDelegate(IntPtr target, in FixedString64Bytes property);

    public interface IViewModelBindingNotify : IViewModelBinding, INotifyBindablePropertyChanged
    {
        void OnPropertyChanged(in FixedString64Bytes property);

        internal static class Active
        {
            public static readonly Dictionary<IntPtr, IViewModelBindingNotify> Objects = new();

            public static FunctionPointer<OnPropertyChangedDelegate> Notify;

            static Active()
            {
                Notify = new FunctionPointer<OnPropertyChangedDelegate>(Marshal.GetFunctionPointerForDelegate<OnPropertyChangedDelegate>(NotifyForwarding));
            }

            [AOT.MonoPInvokeCallback(typeof(OnPropertyChangedDelegate))]
            private static void NotifyForwarding(IntPtr target, in FixedString64Bytes property)
            {
                if (Objects.TryGetValue(target, out var notify))
                {
                    notify.OnPropertyChanged(property);
                }
            }
        }
    }

    public interface IViewModelBindingNotify<T> : IViewModelBinding<T>, IViewModelBindingNotify
        where T : unmanaged, IModelBindingNotify
    {
        public static unsafe void Load(IViewModelBindingNotify<T> viewModelBindingNotify)
        {
            var ptr = (IntPtr)UnsafeUtility.AddressOf(ref viewModelBindingNotify.Value);
            Active.Objects[ptr] = viewModelBindingNotify;
            viewModelBindingNotify.Value.Notify = Active.Notify;
        }

        public static unsafe void Unload(IViewModelBindingNotify<T> viewModelBindingNotify)
        {
            var ptr = (IntPtr)UnsafeUtility.AddressOf(ref viewModelBindingNotify.Value);
            Active.Objects.Remove(ptr);
            viewModelBindingNotify.Value.Notify = default;
        }

        /// <inheritdoc/>
        void IViewModelBinding.Load()
        {
            Load(this);
        }

        /// <inheritdoc/>
        void IViewModelBinding.Unload()
        {
            Unload(this);
        }
    }
}
#endif