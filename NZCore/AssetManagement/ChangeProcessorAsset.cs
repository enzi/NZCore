// <copyright project="NZCore" file="ChangeProcessorAsset.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using UnityEngine;

namespace NZCore.AssetManagement
{
    public abstract class ChangeProcessorAsset : ScriptableObject //, IChangeProcessor
    {
        public abstract bool HasChanges(List<ChangeProcessorAsset> allAssets);
        public abstract void ProcessChanges(List<ChangeProcessorAsset> allAssets);
    }
}