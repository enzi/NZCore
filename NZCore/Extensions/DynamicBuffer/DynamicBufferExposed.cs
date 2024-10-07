// <copyright project="NZCore" file="DynamicBufferExposed.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    [StructLayout(LayoutKind.Sequential)]
    // ReSharper disable once UnusedTypeParameter
    public unsafe struct DynamicBufferExposed<T>
    {
        [NativeDisableUnsafePtrRestriction] [NoAlias]
        internal BufferHeader* m_Buffer;

        // Stores original internal capacity of the buffer header, so heap excess can be removed entirely when trimming.
        private int m_InternalCapacity;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety0;
        internal AtomicSafetyHandle m_Safety1;
        internal int m_SafetyReadOnlyCount;
        internal int m_SafetyReadWriteCount;

        [MarshalAs(UnmanagedType.U1)] internal bool m_IsReadOnly;

        [MarshalAs(UnmanagedType.U1)] internal bool m_useMemoryInitPattern;
        internal byte m_memoryInitPattern;
#endif
    }
}