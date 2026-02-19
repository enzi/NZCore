// <copyright project="NZCore.Editor" file="AssetDatabaseUtility.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NZCore.Editor
{
    public static class AssetDatabaseUtility
    {
        public static T[] GetAssets<T>()
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                                            .Select(AssetDatabase.GUIDToAssetPath)
                                            .Distinct()
                                            .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                                            .Where(s => s.GetType() == typeof(T))
                                            .Cast<T>()
                                            .ToArray();
        }
        
        public static List<ScriptableObject> GetSubAssets(this Object asset)
        {
            List<ScriptableObject> assets = new List<ScriptableObject>();
            var assetPathWithName = AssetDatabase.GetAssetPath(asset);
            var childAssets = AssetDatabase.LoadAllAssetsAtPath(assetPathWithName);

            if (childAssets.Length <= 1)
                return assets;
            
            foreach (var childAsset in childAssets)
            {
                if (childAsset is ScriptableObject so)
                {
                    assets.Add(so);
                }
            }

            return assets;
        }
        
        public static bool TryGetSubAsset<T>(this Object asset, out T result)
            where T : ScriptableObject
        {
            var assetPathWithName = AssetDatabase.GetAssetPath(asset);
            var childAssets = AssetDatabase.LoadAllAssetsAtPath(assetPathWithName);
            
            foreach (var childAsset in childAssets)
            {
                if (childAsset is not T found)
                {
                    continue;
                }

                result = found;
                return true;
            }

            result = null;
            return false;
        }
        
        public static List<ScriptableObject> GetSubAssets(string baseType)
        {
            List<ScriptableObject> assets = new List<ScriptableObject>();
            List<string> assetPathList = AssetDatabase.FindAssets($"t:{baseType}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();

            for (var i = 0; i < assetPathList.Count; i++)
            {
                var assetPath = assetPathList[i];
                var tmpAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

                if (tmpAssets.Length > 1)
                {
                    foreach (var tmpAsset in tmpAssets)
                    {
                        if (tmpAsset != null && tmpAsset.GetType().Name == baseType)
                        {
                            assets.Add((ScriptableObject)tmpAsset);
                        }
                    }
                }
                else
                {
                    assets.Add((ScriptableObject)tmpAssets[0]);
                }
            }

            return assets;
        }
        
        public static List<ScriptableObject> GetSubAssets(Type baseType)
        {
            List<ScriptableObject> assets = new List<ScriptableObject>();
            List<string> assetPathList = AssetDatabase.FindAssets($"t:{baseType.Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();

            for (var i = 0; i < assetPathList.Count; i++)
            {
                var assetPath = assetPathList[i];
                var tmpAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

                if (tmpAssets.Length > 1)
                {
                    foreach (var tmpAsset in tmpAssets)
                    {
                        if (tmpAsset != null && baseType.IsAssignableFrom(tmpAsset.GetType()))
                        {
                            assets.Add((ScriptableObject)tmpAsset);
                        }
                    }
                }
                else
                {
                    assets.Add((ScriptableObject)tmpAssets[0]);
                }
            }

            return assets;
        }

        public static List<TSubAsset> GetFilteredSubAssets<TAsset, TSubAsset>(
            TAsset asset,
            params string[] nameFilter)
            where TAsset : ScriptableObject
            where TSubAsset : ScriptableObject
        {
            List<TSubAsset> list = new List<TSubAsset>();
            var assetPathWithName = AssetDatabase.GetAssetPath(asset);
            var childAssets = AssetDatabase.LoadAllAssetsAtPath(assetPathWithName);

            foreach (var filter in nameFilter)
            {
                if (childAssets.TryGetSubAssetExists(filter, out var existingAsset))
                {
                   list.Add((TSubAsset) existingAsset);
                }
            }

            return list;
        }

        public static void CreateOrUpdateSubAssets<TAsset, TSubAsset>(
            TAsset asset,
            Action<TAsset, SerializedObject> setData,
            params string[] nameFilter)
            where TAsset : ScriptableObject
            where TSubAsset : ScriptableObject
        {
            var assetPathWithName = AssetDatabase.GetAssetPath(asset);
            var childAssets = AssetDatabase.LoadAllAssetsAtPath(assetPathWithName);

            foreach (var filter in nameFilter)
            {
                string nameAndFilter = $"{asset.name}{filter}";
                if (childAssets.TryGetSubAssetExists(nameAndFilter, out var existingAsset))
                {
                    var so = new SerializedObject(existingAsset);
                    setData(asset, so);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    var subAsset = ScriptableObject.CreateInstance<TSubAsset>();
                    subAsset.name = nameAndFilter;

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