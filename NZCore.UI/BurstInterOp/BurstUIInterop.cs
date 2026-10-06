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
    
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class BurstUIInterop
    {
        public static readonly Dictionary<IntPtr, IViewModelBindingNotify> Objects = new();

        static BurstUIInterop()
        {
            // CoreCLR can free the function pointer. alloc to keep it alive
            NotifyDelegate notifyForwarding = NotifyForwarding;
            GCHandle.Alloc(notifyForwarding);
            BurstUINotify.Notify.Data = new FunctionPointer<NotifyDelegate>(Marshal.GetFunctionPointerForDelegate(notifyForwarding));
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