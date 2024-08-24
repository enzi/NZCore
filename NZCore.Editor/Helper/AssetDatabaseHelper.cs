// <copyright project="NZCore" file="AssetDatabaseHelper.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

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
                    for (var i = 0; i < tmpAssets.Length; i++)
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

        public static void CreateOrUpdateSubAssets<TAsset, TSubAsset>(
            TAsset asset,
            Action<TAsset, SerializedObject> setData,
            params string[] subAssetPostfix)
            where TAsset : ScriptableObject
            where TSubAsset : ScriptableObject
        {
            var assetPathWithName = AssetDatabase.GetAssetPath(asset);
            var childAssets = AssetDatabase.LoadAllAssetsAtPath(assetPathWithName);

            foreach (var type in subAssetPostfix)
            {
                if (childAssets.TryGetSubAssetExists(type, out var existingAsset))
                {
                    var so = new SerializedObject(existingAsset);
                    setData(asset, so);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    var subAsset = ScriptableObject.CreateInstance<TSubAsset>();
                    subAsset.name = $"{asset.name}{type}";

                    var so = new SerializedObject(subAsset);
                    setData(asset, so);

                    so.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.AddObjectToAsset(subAsset, asset);
                }

                AssetDatabase.SaveAssets();
            }
        }

        public static bool SubAssetExists(this Object[] childAssets, string nameFilter)
        {
            foreach (var childAsset in childAssets)
            {
                if (AssetDatabase.IsSubAsset(childAsset) && childAsset.name.Contains(nameFilter))
                    return true;
            }

            return false;
        }

        public static bool TryGetSubAssetExists(this Object[] childAssets, string nameFilter, out Object subAsset)
        {
            foreach (var childAsset in childAssets)
            {
                if (AssetDatabase.IsSubAsset(childAsset) && childAsset.name.Contains(nameFilter))
                {
                    subAsset = childAsset;
                    return true;
                }
            }

            subAsset = null;
            return false;
        }

        public static void DeleteSubAssets(this Object[] childAssets)
        {
            foreach (var childAsset in childAssets)
            {
                if (AssetDatabase.IsSubAsset(childAsset))
                {
                    AssetDatabase.RemoveObjectFromAsset(childAsset);
                }
            }

            AssetDatabase.SaveAssets();
        }

        public static void MarkAndSave(Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            AssetDatabase.Refresh();
        }
    }
}