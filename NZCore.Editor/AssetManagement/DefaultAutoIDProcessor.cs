using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
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
            if (didDomainReload || importedAssets.Length == 0)
            {
                return;
            }

            ProcessDefaultAutoIDs(importedAssets);
        }

        private static void ProcessDefaultAutoIDs(string[] importedAssets)
        {
            List<DefaultAutoIDData> newDataList = new List<DefaultAutoIDData>();

            foreach (var assetPath in importedAssets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

                if (asset == null || asset is not ScriptableObjectWithAutoID autoID || asset is not IDefaultAutoID defaultAutoID)
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
                        DataType = defaultAutoID.DefaultType.Name,
                        DefaultValue = defaultAutoID.AutoID,
                        StructName = defaultAutoID.DefaultType.Name,
                        IsPrimitive = defaultAutoID.DefaultType.IsPrimitive,
                        AssetType = assetType
                    });
                }
            }

            if (newDataList.Count != 1)
            {
                return;
            }

            var data = newDataList[0];

            var attribute = data.AssetType.GetCustomAttributeRecursive<DefaultAutoIDPathAttribute>(out var baseType);
            if (attribute != null)
            {
                var json = JsonConvert.SerializeObject(newDataList[0]);
                var csVersion = $"/*{json}*/";
                var path = $"Packages/com.nzspellcasting/{attribute.Path}";
                var cscPath = $"{path}/csc.rsp";
                var jsonPath = $"{path}/{data.StructName}.settings.cs";
                var resolvedJsonPath = Path.GetFullPath(jsonPath);

                File.WriteAllText(jsonPath, csVersion);

                if (CompilerServiceUtility.AddAdditionalFiles(cscPath, resolvedJsonPath))
                {
                    // trigger a compile ?
                }
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
            public string DataType;
            public int DefaultValue;
            public bool IsPrimitive;
        }
    }
}