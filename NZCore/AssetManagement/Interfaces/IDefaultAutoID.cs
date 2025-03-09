// <copyright project="NZCore" file="IDefaultAutoID.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.AssetManagement.Interfaces
{
    public interface IDefaultAutoID
    {
        public bool Default { get; }

        public Type DefaultType { get; }
    }
}