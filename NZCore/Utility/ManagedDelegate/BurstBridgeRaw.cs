// <copyright project="NZCore" file="ManagedDelegateRaw.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    public readonly unsafe struct BurstBridgeRaw
    {
        [NativeDisableUnsafePtrRestriction] private readonly IntPtr _fnPtr;
        [NativeDisableUnsafePtrRestriction] private readonly IntPtr _wrapperPtr;

        public bool IsCreated => _fnPtr != default;

        public BurstBridgeRaw(delegate*<void*, int, void> callback)
        {
            BurstBridgeWrappers.EnsureRawWire();
            _fnPtr = (IntPtr)callback;
            _wrapperPtr = BurstBridgeWrappers.RawWirePtr;
        }

        public void Invoke(void* argsPtr, int argsSize)
        {
            if (_fnPtr == default)
            {
                throw new NullReferenceException($"Invoking a null BurstBridgeRaw!");
            }
            
            ((delegate* unmanaged[Cdecl]<void*, void*, int, void>)_wrapperPtr)(
                (void*)_fnPtr, argsPtr, argsSize);
        }
        
        public void Invoke<T>(ref T arguments)
            where T : unmanaged
        {
            fixed (T* argumentsPtr = &arguments)
            {
                Invoke(argumentsPtr, UnsafeUtility.SizeOf<T>());
            }
        }
        
        public static ref T ArgumentsFromPtr<T>(void* argumentsPtr, int size)
            where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (size != UnsafeUtility.SizeOf<T>())
            {
                throw new InvalidOperationException("The requested argument type size does not match the provided one.");
            }
#endif
            return ref *(T*)argumentsPtr;
        }

    }
}