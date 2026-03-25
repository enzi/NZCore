// <copyright project="NZCore" file="ManagedDelegate.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    public readonly unsafe struct BurstBridge
    {
        [NativeDisableUnsafePtrRestriction] private readonly IntPtr _fnPtr;
        [NativeDisableUnsafePtrRestriction] private readonly IntPtr _wrapperPtr;

        public bool IsCreated => _fnPtr != default;

        public BurstBridge(delegate*<void> callback)
        {
            BurstBridgeWrappers.EnsureParamless();
            
            _fnPtr      = (IntPtr) callback;
            _wrapperPtr = BurstBridgeWrappers.ParamlessPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke()
        {
            CheckCreated();
            ((delegate* unmanaged[Cdecl]<void*, void>)_wrapperPtr)((void*)_fnPtr);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckCreated()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (_fnPtr == default)
            {
                throw new NullReferenceException("Invoking a null ManagedDelegate.");
            }
#endif
        }
    }
}