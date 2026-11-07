// <copyright project="NZCore.Authoring" file="AutoIDConflicts.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using Object = UnityEngine.Object;

[assembly: InternalsVisibleTo("NZCore.Tests")]
[assembly: InternalsVisibleTo("NZCore.Tests.Authoring")]

namespace NZCore.AssetManagement
{
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    internal static partial class AutoIDConflicts
    {
        private static readonly Dictionary<Type, Dictionary<int, List<Object>>> HoldersByScanType = new();

        public static Dictionary<int, List<Object>> Collect(Type scanType)
        {
            var holders = new Dictionary<int, List<Object>>();
            var assetPaths = AssetDatabase.FindAssets($"t:{scanType.Name}").Select(AssetDatabase.GUIDToAssetPath).Distinct();

            foreach (var assetPath in assetPaths)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                {
                    // account for sub assets
                    if (asset == null || !scanType.IsAssignableFrom(asset.GetType()))
                    {
                        continue;
                    }

                    var id = ((IAutoID)asset).AutoID;

                    if (!holders.TryGetValue(id, out var list))
                    {
                        holders[id] = list = new List<Object>();
                    }

                    list.Add(asset);
                }
            }

            return holders;
        }

        public static bool TryGetOthers(ScriptableObjectWithAutoID asset, out List<Object> others)
        {
            var scanType = AutoIDProcessorExtensions.ResolveScanType(asset.GetType(), out _);

            if (!HoldersByScanType.TryGetValue(scanType, out var holders))
            {
                holders = HoldersByScanType[scanType] = Collect(scanType);
            }

            others = holders.TryGetValue(asset.AutoID, out var list) ? list.Where(holder => holder != asset).ToList() : new List<Object>();
            return others.Count > 0;
        }

        public static void AssignNewId(ScriptableObjectWithAutoID asset)
        {
            asset.AutoID = 0;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        public static void Clear()
        {
            HoldersByScanType.Clear();
        }
    }
}