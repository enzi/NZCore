// <copyright project="NZCore" file="IAutoIDProcessor.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore.AssetManagement
{
    public interface IAutoIDProcessor
    {
        void Process(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths);
    }
}