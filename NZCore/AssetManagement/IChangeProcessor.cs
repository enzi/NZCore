// <copyright project="NZCore" file="IChangeProcessor.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;

namespace NZCore.AssetManagement
{
    public interface IChangeProcessor
    {
        Type ProcessGroupType { get; }
        HasChangeResult HasChanges(List<IChangeProcessor> allAssets);
        void ProcessChanges(List<IChangeProcessor> allAssets);
    }

    public enum HasChangeResult
    {
        None,
        NoChanges,
        HasChanges
    }
}
