// <copyright project="NZCore" file="IPointer.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore.Interfaces
{
    public unsafe interface IPointer
    {
        public byte* Ptr { get; }
    }
}