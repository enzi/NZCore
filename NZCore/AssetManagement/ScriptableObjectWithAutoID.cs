// <copyright project="NZCore" file="ScriptableObjectWithAutoID.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore.AssetManagement
{
    public abstract class ScriptableObjectWithAutoID : ChangeProcessorAsset, IAutoID
    {
        public abstract int AutoID { get; set; }
    }
}