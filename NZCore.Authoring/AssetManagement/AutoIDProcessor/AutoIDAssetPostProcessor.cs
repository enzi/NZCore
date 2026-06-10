// <copyright project="NZCore.Editor" file="AutoIDProcessor.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using NZCore.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NZCore.AssetManagement
{
    public class AutoIDAssetEvents : AssetModificationProcessor
    {
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

            if (asset == null)
            {
                return AssetDeleteResult.DidNotDelete;
            }

            ScriptableObjectDatabase.DeleteAsset(asset);

            return AssetDeleteResult.DidNotDelete;
        }
    }

    public class AutoIDAssetPostProcessor : AssetPostprocessor
    {
        [UsedImplicitly]
        // ReSharper disable once Unity.IncorrectMethodSignature
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            if (didDomainReload || (importedAssets.Length == 0 && deletedAssets.Length == 0))
            {
                return;
            }

            //Debug.Log("OnPostprocessAllAssets");

            var processors = new Dictionary<Type, AutoIDProcessor>();

            foreach (var assetPath in importedAssets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

                if (asset == null)
                {
                    continue;
                }

                ProcessAsset(processors, asset);

                foreach (var subAsset in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
                {
                    ProcessAsset(processors, subAsset);
                }
            }

            foreach (var processor in processors)
            {
                if (processor.Value.RegisteredType != null)
                {
                    ScriptableObjectDatabase.Update(processor.Value.RegisteredType);
                }
            }
        }
        
        internal static void ProcessAsset(Dictionary<Type, AutoIDProcessor> processors, Object asset)
        {
            if (processors.TryGetProcessor(asset, out var processor))
            {
                processor.Process(asset);
            }
        }
    }


    internal class AutoIDProcessor
    {
        public readonly Type ScanType;       // drives the asset filter and shared autoID pool (may be a group base)
        public Type RegisteredType;          // concrete attribute-bearing type seen in this batch; null if none
        private readonly string _filter;
        private Dictionary<int, int> _map;

        public AutoIDProcessor(Type scanType)
        {
            ScanType = scanType;
            _filter = $"t:{scanType.Name}";
        }

        private Dictionary<int, int> CreateMap()
        {
            var tmpMap = new Dictionary<int, int>();
            var assetPaths = AssetDatabase.FindAssets(_filter).Select(AssetDatabase.GUIDToAssetPath).Distinct();

            foreach (var assetPath in assetPaths)
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

                foreach (var asset in assets)
                {
                    // account for sub assets
                    if (asset == null || !ScanType.IsAssignableFrom(asset.GetType()))
                    {
                        continue;
                    }

                    var id = ((IAutoID)asset).AutoID;
                    tmpMap.TryGetValue(id, out var count);
                    count++;
                    tmpMap[id] = count;
                }
            }

            return tmpMap;
        }

        public void Process(Object asset)
        {
            _map ??= CreateMap();

            var autoIdAsset = (IAutoID)asset;

            if (_map.TryGetValue(autoIdAsset.AutoID, out var count) && (autoIdAsset.AutoID == 0 || count > 1))
            {
                var newId = GetFirstFreeID(_map);

                if (newId == -1)
                {
                    Debug.LogError("AutoIDProcessor has run out of IDs!");
                    return;
                }

                _map[autoIdAsset.AutoID] = count - 1;
                autoIdAsset.AutoID = newId;
                _map[newId] = 1;

                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }

        private static int GetFirstFreeID(Dictionary<int, int> map)
        {
            // we start at 1 and reserve 0 for "None" states
            for (var i = 1; i < int.MaxValue; i++)
            {
                if (!map.ContainsKey(i))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    internal static class AutoIDProcessorExtensions
    {
        public static bool TryGetProcessor(this ScriptableObject asset, out AutoIDProcessor processor)
        {
            if (asset is not ScriptableObjectWithAutoID)
            {
                processor = null;
                return false;
            }

            var scanType = ResolveScanType(asset.GetType(), out _);
            processor = new AutoIDProcessor(scanType);

            return true;
        }

        public static bool TryGetProcessor(this Dictionary<Type, AutoIDProcessor> processors, Object asset, out AutoIDProcessor processor)
        {
            if (asset is not ScriptableObjectWithAutoID)
            {
                processor = null;
                return false;
            }

            var assetType = asset.GetType();
            var scanType = ResolveScanType(assetType, out var attribute);

            if (!processors.TryGetValue(scanType, out processor))
            {
                processor = processors[scanType] = new AutoIDProcessor(scanType);
            }
            
            if (attribute != null)
            {
                processor.RegisteredType = assetType;
            }

            return true;
        }

        private static Type ResolveScanType(Type assetType, out RegisterInScriptableObjectDatabaseAttribute attribute)
        {
            attribute = assetType.GetCustomAttribute<RegisterInScriptableObjectDatabaseAttribute>();
            return attribute?.GroupByType ?? ReflectionUtility.GetRootType<ScriptableObjectWithAutoID>(assetType);
        }
    }
}