// <copyright project="NZCore.Editor" file="AdditionalFileSignaturePostprocessor.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Linq;
using UnityEditor;

namespace NZCore.Editor
{
    /// <summary>
    /// Keeps the signature of every signed data file ({name}.{generator}.data) in sync with its content, also when the file is replaced or moved by hand,
    /// so the generators reading it rerun. See <see cref="CompilerServiceUtility"/>.
    /// </summary>
    internal class AdditionalFileSignaturePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var assetPath in importedAssets.Concat(movedAssets).Distinct())
            {
                if (CompilerServiceUtility.IsSignedDataFile(assetPath))
                {
                    CompilerServiceUtility.WriteSignature(assetPath);
                }
            }
        }
    }
}