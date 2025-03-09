// <copyright project="NZCore" file="BufferAccessorExposed.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    [StructLayout(LayoutKind.Sequential)]
    // ReSharper disable once UnusedTypeParameter
    public unsafe struct BufferAccessorExposed<T>
        where T : unmanaged, IBufferElementData
    {
        [NativeDisableUnsafePtrRestriction] public byte* m_BasePointer;
        public int m_Length;
        public int m_Stride;
        public int m_InternalCapacity;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private byte m_IsReadOnly;
        private AtomicSafetyHandle m_Safety0;
        private AtomicSafetyHandle m_ArrayInvalidationSafety;
        private int m_SafetyReadOnlyCount;
        private int m_SafetyReadWriteCount;
#endif
    }
}