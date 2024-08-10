// <copyright project="NZCore" file="ScriptableObjectWithAutoID.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using UnityEngine;

namespace NZCore.AssetManagement
{
    public abstract class ScriptableObjectWithAutoID : ChangeProcessorAsset, IAutoID
    {
        public abstract int AutoID { get; set; }
    }
}