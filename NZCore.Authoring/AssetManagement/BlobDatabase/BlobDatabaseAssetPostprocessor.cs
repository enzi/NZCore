// <copyright project="NZCore.Authoring" file="BlobDatabaseAssetPostprocessor.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace NZCore.Authoring
{
    /// <summary>
    /// Handles triggering <see cref="ScriptableObjectDatabaseBaker"/> via bumping the version.
    /// Only happens for created/deleted assets
    /// </summary>
    public class BlobDatabaseAssetPostprocessor : AssetPostprocessor
    {
        private static bool pendingUpdate;

        [UsedImplicitly]
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (didDomainReload)
            {
                return;
            }

            bool needsUpdate = false;

            for (int i = 0; i < deletedAssets.Length && !needsUpdate; i++)
            {
                needsUpdate = deletedAssets[i].EndsWith(".asset");
            }

            if (!needsUpdate)
            {
                foreach (var assetPath in importedAssets)
                {
                    if (!assetPath.EndsWith(".asset"))
                    {
                        continue;
                    }

                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                    if (asset is IConvertToBlob)
                    {
                        needsUpdate = true;
                        break;
                    }
                }
            }

            if (!needsUpdate || pendingUpdate)
            {
                return;
            }

            pendingUpdate = true;
            EditorApplication.delayCall += BumpBlobDatabaseVersion;
        }

        private static void BumpBlobDatabaseVersion()
        {
            pendingUpdate = false;

            var guids = AssetDatabase.FindAssets("t:" + nameof(ScriptableObjectDatabaseVersion));
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var version = AssetDatabase.LoadAssetAtPath<ScriptableObjectDatabaseVersion>(path);
                if (version == null)
                {
                    continue;
                }

                version.Version++;
                EditorUtility.SetDirty(version);
                AssetDatabase.SaveAssetIfDirty(version);
            }
        }
    }
}