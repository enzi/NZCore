// <copyright project="NZCore" file="IAutoIDProcessor.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore.AssetManagement
{
    public interface IAutoIDProcessor
    {
        void Process(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths);
    }
}