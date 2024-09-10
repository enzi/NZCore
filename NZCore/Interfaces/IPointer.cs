// <copyright project="NZCore" file="IPointer.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore.Interfaces
{
    public unsafe interface IPointer
    {
        public byte* Ptr { get; }
    }
}