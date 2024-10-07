// <copyright project="NZCore" file="IPointer.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore.Interfaces
{
    public unsafe interface IPointer
    {
        public byte* Ptr { get; }
    }
}