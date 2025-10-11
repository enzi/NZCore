// <copyright project="NZCore" file="ManagedDelegate.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;
using AOT;
using Unity.Burst;

namespace NZCore
{
    public readonly struct ManagedDelegate<T> : IDisposable
        where T : Delegate
    {
        public readonly FunctionPointer<T> Ptr;
        private readonly GCHandle handle;

        public ManagedDelegate(T function)
        {
#if UNITY_EDITOR
            if (!function.Method.IsStatic)
            {
                throw new Exception("Function needs to be static!");
            }
            
            if (function.Method.GetCustomAttributes(typeof(MonoPInvokeCallbackAttribute), false).Length == 0)
            {
                throw new Exception("Function is missing MonoPInvokeCallback attribute!");
            }
#endif
            handle = GCHandle.Alloc(function);
            Ptr = new FunctionPointer<T>(Marshal.GetFunctionPointerForDelegate(function));
        }

        public void Dispose()
        {
            handle.Free();
        }
    }
}