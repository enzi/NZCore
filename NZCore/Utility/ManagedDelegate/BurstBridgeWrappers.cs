// <copyright project="NZCore" file="ManagedDelegateWrappers.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;
using AOT;

namespace NZCore
{
    internal static unsafe class BurstBridgeWrappers
    {
        // parameterless
        internal static IntPtr ParamlessPtr;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ParamlessWrapperDelegate(void* fnPtr);

        internal static void EnsureParamless()
        {
            if (ParamlessPtr != default)
            {
                return;
            }

            ParamlessWrapperDelegate d = ParamlessWrapper;
            GCHandle.Alloc(d);
            ParamlessPtr = Marshal.GetFunctionPointerForDelegate(d);
        }

        [MonoPInvokeCallback(typeof(ParamlessWrapperDelegate))]
        private static void ParamlessWrapper(void* fnPtr)
        {
            ((delegate*<void>)fnPtr)();
        }

        // raw wire (void* fnPtr, void* argsPtr, int argsSize)
        internal static IntPtr RawWirePtr;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void RawWireWrapperDelegate(void* fnPtr, void* argsPtr, int argsSize);

        internal static void EnsureRawWire()
        {
            if (RawWirePtr != default)
            {
                return;
            }

            RawWireWrapperDelegate d = RawWireWrapper;
            GCHandle.Alloc(d);
            RawWirePtr = Marshal.GetFunctionPointerForDelegate(d);
        }

        [MonoPInvokeCallback(typeof(RawWireWrapperDelegate))]
        private static void RawWireWrapper(void* fnPtr, void* argsPtr, int argsSize)
        {
            ((delegate*<void*, int, void>)fnPtr)(argsPtr, argsSize);
        }
    }
}