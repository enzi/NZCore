// <copyright project="NZCore.Editor" file="DefaultAutoIDProcessor.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NZCore.AssetManagement;
using NZCore.AssetManagement.Interfaces;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NZCore.Editor.AssetManagement
{
    public class DefaultAutoIDProcessor : AssetPostprocessor
    {
        [UsedImplicitly]
        // ReSharper disable once Unity.IncorrectMethodSignature
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (didDomainReload || (importedAssets.Length == 0 && deletedAssets.Length != 0))
            {
                return;
            }

            ProcessDefaultAutoIDs(importedAssets);
        }

        private static void ProcessDefaultAutoIDs(string[] importedAssets)
        {
            foreach (var assetPath in importedAssets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

                if (asset == null || asset is not ScriptableObjectWithAutoID || asset is not IDefaultAutoID)
                {
                    continue;
                }

                var assetType = asset.GetType();
                ProcessDefaultAutoIDs(assetType);
            }
        }

        public static void ProcessDefaultAutoIDs(Type assetType)
        {
            List<DefaultAutoIDData> newDataList = new List<DefaultAutoIDData>();

            List<Object> objects = AssetDatabase.FindAssets($"t:{assetType.Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .Where(s => s.GetType() == assetType)
                .ToList();

            foreach (var obj in objects)
            {
                if (obj is not ScriptableObjectWithDefaultAutoID defaultAutoID)
                {
                    continue;
                }

                if (defaultAutoID.Default)
                {
                    newDataList.Add(new DefaultAutoIDData()
                    {
                        StructName = defaultAutoID.DefaultType.Name,
                        AssetType = assetType,
                        
                        DefaultValue = defaultAutoID.AutoID
                    });
                }
            }

            if (newDataList.Count != 1)
            {
                return;
            }

            var data = newDataList[0];

            var attribute = data.AssetType.GetCustomAttributeRecursive<PackagePathAttribute>(out _);
            var cscAttribute = data.AssetType.GetCustomAttributeRecursive<CscPathAttribute>(out _);

            if (attribute != null)
            {
                var path = attribute.AddUniqueSettingsPath ? $"{attribute.Path}/{CompilerServiceUtility.GetUniqueSettingsPath()}" : attribute.Path;
                CompilerServiceUtility.WriteJson(data, data.StructName, path, cscAttribute != null ? cscAttribute.Path : new[] { attribute.Path });
            }
            else
            {
                Debug.LogError("IDefaultAutoID also requires a DefaultAutoIDPath attribute!");
            }
        }

        public class DefaultAutoIDData
        {
            [NonSerialized] public Type AssetType;
            public string StructName;
            
            // used for codegen
            [UsedImplicitly] public int DefaultValue;
        }
    }
}