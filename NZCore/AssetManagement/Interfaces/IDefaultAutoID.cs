// <copyright project="NZCore" file="IDefaultAutoID.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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