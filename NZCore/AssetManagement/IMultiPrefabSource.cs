// <copyright project="NZCore" file="IMultiPrefabSource.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;

namespace NZCore.AssetManagement
{
    public interface IMultiPrefabSource
    {
        IEnumerable<string> PrefabNames { get; }
    }
}