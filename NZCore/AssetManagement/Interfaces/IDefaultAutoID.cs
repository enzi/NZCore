// <copyright project="NZCore" file="IDefaultAutoID.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
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