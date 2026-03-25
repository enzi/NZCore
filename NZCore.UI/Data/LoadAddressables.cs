// <copyright project="NZCore.UI" file="LoadAddressables.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Unity.Entities;

namespace NZCore.UIToolkit.Data
{
    public struct LoadAddressablesRequest : IComponentData
    {
        public bool PrintLoadedAssets;
    }

    public class LoadCustomUIAssetsRequest : IComponentData
    {
        public List<CustomUIAsset> CustomAssets = new();
    }
}