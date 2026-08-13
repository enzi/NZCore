// <copyright project="NZCore" file="ScriptableObjectWithAutoID.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NZCore.AssetManagement
{
    public abstract class ScriptableObjectWithAutoID : ScriptableObject, IAutoID, IChangeProcessor
    {
        public abstract int AutoID { get; set; }
        public virtual Type ProcessGroupType => GetType();
        public abstract HasChangeResult HasChanges(List<IChangeProcessor> allAssets);
        public abstract void ProcessChanges(List<IChangeProcessor> allAssets);
    }
}
