// <copyright project="NZCore" file="ChangeProcessorAsset.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using UnityEngine;

namespace NZCore.AssetManagement
{
    public abstract class ChangeProcessorAsset : ScriptableObject
    {
        public abstract HasChangeResult HasChanges(List<ChangeProcessorAsset> allAssets);
        public abstract void ProcessChanges(List<ChangeProcessorAsset> allAssets);
    }

    public enum HasChangeResult
    {
        None, // won't even show the ChangeProcessorAsset buttons
        NoChanges,
        HasChanges
    }
}