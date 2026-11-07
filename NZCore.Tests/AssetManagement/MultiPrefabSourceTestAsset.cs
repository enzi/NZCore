// <copyright project="NZCore.Tests" file="MultiPrefabSourceTestAsset.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using NZCore.AssetManagement;
using UnityEngine;

namespace NZCore.Tests.AssetManagement
{
    public class MultiPrefabSourceTestAsset : ScriptableObject, IMultiPrefabSource
    {
        public List<string> Names = new();

        public IEnumerable<string> PrefabNames => Names;
    }
}