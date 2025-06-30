// <copyright project="NZCore" file="PointerStructs.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    public unsafe struct EntityPointer
    {
        public Entity* Ptr;
    }
    
    public unsafe struct BytePointer
    {
        public byte* Ptr;
    }
    
    public unsafe struct VoidPointer
    {
        public void* Ptr;
    }
}