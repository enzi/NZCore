// <copyright project="NZCore.MVVM" file="IModelBinding.cs" version="1.0.0">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

#if UNITY_6000
namespace NZCore.UI
{
    public interface IModelBinding { }
    public interface IModelBindingNotify : IModelBinding { }

    public static class BindingObjectNotifyDataExtensions
    {
        public static unsafe void Notify<T>(this ref T binding, [CallerMemberName] string property = "")
            where T : unmanaged, IModelBindingNotify
        {
            if (BurstUINotify.Notify.Data.IsCreated)
            {
                BurstUINotify.Notify.Data.Invoke((IntPtr)UnsafeUtility.AddressOf(ref binding), property);
            }
        }

        public static unsafe void NotifyExplicit<T>(this ref T binding, FixedString64Bytes property)
            where T : unmanaged, IModelBindingNotify
        {
            if (BurstUINotify.Notify.Data.IsCreated)
            {
                BurstUINotify.Notify.Data.Invoke((IntPtr)UnsafeUtility.AddressOf(ref binding), property);
            }
        }
    }
}
#endif