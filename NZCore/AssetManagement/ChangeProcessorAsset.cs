// <copyright project="NZCore" file="ChangeProcessorAsset.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NZCore.AssetManagement
{
    public abstract class ChangeProcessorAsset : ScriptableObject
    {
        public abstract HasChangeResult HasChanges(List<ChangeProcessorAsset> allAssets);
        public abstract void ProcessChanges(List<ChangeProcessorAsset> allAssets);

        // Editor-side grouping key: subclasses that share a ProcessChanges destination
        // (like write to the same JSON file) should override this to return a common base type,
        // so the editor gathers and processes all of them together instead of overwriting each other.
        public virtual Type ProcessGroupType => GetType();
    }

    public enum HasChangeResult
    {
        None, // won't even show the ChangeProcessorAsset buttons
        NoChanges,
        HasChanges
    }
}