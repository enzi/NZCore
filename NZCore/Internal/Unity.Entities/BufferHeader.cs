// <copyright project="Unity.Entities" file="BufferHeader.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using Unity.Burst;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Keep the field offsets identical to Unity's header; all capacity changes delegate to Unity.
    [StructLayout(LayoutKind.Explicit)]
    [NoAlias]
    public unsafe struct BufferHeader
    {
        public enum TrashMode
        {
            TrashOldData = (int)UnityEntities.BufferHeader.TrashMode.TrashOldData,
            RetainOldData = (int)UnityEntities.BufferHeader.TrashMode.RetainOldData
        }

        [NoAlias] [FieldOffset(0)] public byte* Pointer;
        [FieldOffset(8)] public int Length;
        [FieldOffset(12)] public int Capacity;

        public static byte* GetElementPointer(BufferHeader* header) => UnityEntities.BufferHeader.GetElementPointer((UnityEntities.BufferHeader*)header);

        public static void EnsureCapacity(BufferHeader* header, int count, int typeSize, int alignment, TrashMode trashMode, bool useMemoryInitPattern,
            byte memoryInitPattern) =>
            UnityEntities.BufferHeader.EnsureCapacity((UnityEntities.BufferHeader*)header, count, typeSize, alignment,
                (UnityEntities.BufferHeader.TrashMode)trashMode, useMemoryInitPattern, memoryInitPattern);

        public static void SetCapacity(BufferHeader* header, int count, int typeSize, int alignment, TrashMode trashMode, bool useMemoryInitPattern,
            byte memoryInitPattern, int internalCapacity) =>
            UnityEntities.BufferHeader.SetCapacity((UnityEntities.BufferHeader*)header, count, typeSize, alignment,
                (UnityEntities.BufferHeader.TrashMode)trashMode, useMemoryInitPattern, memoryInitPattern, internalCapacity);
    }
}