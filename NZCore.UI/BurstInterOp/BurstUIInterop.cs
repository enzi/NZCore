// <copyright project="NZCore.MVVM" file="BurstUIInterop.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;

namespace NZCore.UI
{
    internal delegate void NotifyDelegate(IntPtr target, in FixedString64Bytes property);
    
    internal static class BurstUINotify
    {
        public static readonly SharedStatic<FunctionPointer<NotifyDelegate>> Notify =
            SharedStatic<FunctionPointer<NotifyDelegate>>.GetOrCreate<FunctionPointer<NotifyDelegate>>();
    }
    
    internal static class BurstUIInterop
    {
        public static readonly Dictionary<IntPtr, IViewModelBindingNotify> Objects = new();
        //public static FunctionPointer<OnPropertyChangedDelegate> Notify;

        static BurstUIInterop()
        {
            BurstUINotify.Notify.Data = new FunctionPointer<NotifyDelegate>(Marshal.GetFunctionPointerForDelegate<NotifyDelegate>(NotifyForwarding));
        }

        [AOT.MonoPInvokeCallback(typeof(NotifyDelegate))]
        private static void NotifyForwarding(IntPtr target, in FixedString64Bytes property)
        {
            if (Objects.TryGetValue(target, out var notify))
            {
                notify.OnPropertyChanged(property);
            }
        }
    }

}