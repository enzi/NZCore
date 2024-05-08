using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NZCore.Editor
{
    public static class AssetDatabaseHelper
    {
        public static List<ScriptableObject> GetSubAssets(string baseType)
        {
            List<ScriptableObject> assets = new List<ScriptableObject>();
            List<string> assetPathList = AssetDatabase.FindAssets($"t:{baseType}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();

            foreach (var assetPath in assetPathList)
            {
                var tmpAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

                if (tmpAssets.Length > 1)
                {
                    for (var i = 1; i < tmpAssets.Length; i++)
                    {
                        var tmpAsset = tmpAssets[i];
                        if (tmpAsset.GetType().Name == baseType)
                            assets.Add((ScriptableObject)tmpAsset);
                    }
                }
                else
                {
                    assets.Add((ScriptableObject)tmpAssets[0]);
                }
            }

            return assets;
        }
    }
}