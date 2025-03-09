// <copyright project="NZCore" file="ScriptableObjectWithAutoID.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore.AssetManagement
{
    public abstract class ScriptableObjectWithAutoID : ChangeProcessorAsset, IAutoID
    {
        public abstract int AutoID { get; set; }
    }
}