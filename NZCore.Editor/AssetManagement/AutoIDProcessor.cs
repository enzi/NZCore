// <copyright project="NZCore.Editor" file="AutoIDProcessor.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NZCore.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NZCore.AssetManagement
{
    public class AutoIDProcessor : AssetPostprocessor
    {
        [UsedImplicitly]
        // ReSharper disable once Unity.IncorrectMethodSignature
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (didDomainReload || importedAssets.Length == 0)
            {
                return;
            }

            var processors = new Dictionary<Type, Processor>();

            foreach (var assetPath in importedAssets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

                if (asset == null)
                {
                    continue;
                }

                ProcessAsset(asset, processors);
                foreach (var subAsset in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
                {
                    ProcessAsset(subAsset, processors);
                }
            }

            foreach (var processor in processors)
            {
                ClearManagers(processor.Value.Type);
            }

            foreach (var processor in processors)
            {
                UpdateManager(processor.Value.Type);
            }
        }

        private static void ProcessAsset(Object asset, Dictionary<Type, Processor> processors)
        {
            if (asset is not IAutoID)
            {
                return;
            }

            var assetType = asset.GetType();
            if (!processors.TryGetValue(assetType, out var processor))
            {
                processor = processors[assetType] = new Processor(assetType);
            }

            processor.Process(asset);
        }


        private static bool TryGetManager(Type type, out ScriptableObject manager, out SerializedObject managerObject, out SerializedProperty containerListProperty)
        {
            manager = null;
            managerObject = null;
            containerListProperty = null;

            var attribute = type.GetCustomAttributeRecursive<AutoIDManagerAttribute>(out _);
            if (attribute == null)
            {
                return false;
            }

            var managerGuid = AssetDatabase.FindAssets($"t:{attribute.ManagerType}");

            if (managerGuid.Length == 0)
            {
                Debug.LogError($"No manager found for {attribute.ManagerType}");
                return false;
            }

            if (managerGuid.Length > 1)
            {
                Debug.LogError($"More than one manager found for {attribute.ManagerType}");
                return false;
            }

            manager = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(managerGuid[0]));
            if (manager == null)
            {
                Debug.LogError("Manager wasn't a ScriptableObject");
                return false;
            }

            managerObject = new SerializedObject(manager);
            containerListProperty = managerObject.FindProperty(attribute.ContainerListProperty);
            if (containerListProperty == null)
            {
                Debug.LogError($"Property {attribute.ContainerListProperty} not found for {attribute.ManagerType}");
                return false;
            }

            if (!containerListProperty.isArray)
            {
                Debug.LogError($"Property {attribute.ContainerListProperty} was not type of array for {attribute.ManagerType}");
                return false;
            }

            return true;
        }

        private static void ClearManagers(Type type)
        {
            if (!TryGetManager(type, out var manager, out var managerObject, out var list))
                return;

            list.ClearArray();
            managerObject.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(manager);
        }

        private static void UpdateManager(Type type)
        {
            if (!TryGetManager(type, out var manager, out var managerObject, out var list))
                return;

            var objects = AssetDatabase.FindAssets($"t:{type.Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .Where(s => s.GetType() == type)
                .ToList();

            foreach (var obj in objects)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = obj;
            }

            managerObject.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(manager);
        }

        private class Processor
        {
            public readonly Type Type;
            private readonly string filter;
            private Dictionary<int, int> map;

            public Processor(Type type)
            {
                Type = type;
                filter = $"t:{type.Name}";
            }

            private Dictionary<int, int> CreateMap()
            {
                var tmpMap = new Dictionary<int, int>();
                var assetPaths = AssetDatabase.FindAssets(filter).Select(AssetDatabase.GUIDToAssetPath).Distinct();

                foreach (var assetPath in assetPaths)
                {
                    var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

                    foreach (var asset in assets)
                    {
                        // account for sub assets
                        if (asset == null || asset.GetType() != Type)
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
                map ??= CreateMap();

                var autoIdAsset = (IAutoID)asset;

                if (map.TryGetValue(autoIdAsset.AutoID, out var count) && (autoIdAsset.AutoID == 0 || count > 1))
                {
                    var newId = GetFirstFreeID(map);

                    if (newId == -1)
                    {
                        Debug.LogError("AutoIDProcessor has run out of IDs!");
                        return;
                    }

                    map[autoIdAsset.AutoID] = count - 1;
                    autoIdAsset.AutoID = newId;
                    map[newId] = 1;

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
                        return i;
                }

                return -1;
            }
        }
    }
}