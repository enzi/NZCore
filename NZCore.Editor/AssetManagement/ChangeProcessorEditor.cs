// <copyright project="NZCore.Editor" file="ChangeProcessorEditor.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using NZCore.AssetManagement;
using Unity;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace NZCore.Editor
{
    [CustomEditor(typeof(ChangeProcessorAsset), true)]
    [CanEditMultipleObjects]
    public class ChangeProcessorEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            serializedObject.FillDefaultInspector(root, true);

            var element = new ChangeProcessorEditorElement(target);
            return element.CreateInspectorGUI(root);
        }
    }

    public class ChangeProcessorEditorElement
    {
        private readonly Object _target;

        public ChangeProcessorEditorElement(Object target)
        {
            this._target = target;
        }

        public VisualElement CreateInspectorGUI(VisualElement root)
        {
            var hasChangesResult = ((ChangeProcessorAsset)_target).HasChanges(GetChangeProcessorAssets(_target.GetType()));

            if (hasChangesResult == HasChangeResult.None)
            {
                return root;
            }

            var btn = new Button(Click_CodeGen)
            {
                text = $"Update {_target.GetType().Name} settings JSON {(hasChangesResult == HasChangeResult.HasChanges ? "(*)" : "")}"
            };

            var btn2 = new Button(Click_CodeGenAll)
            {
                text = $"Update every JSON setting {(hasChangesResult == HasChangeResult.HasChanges ? "(*)" : "")}"
            };

            root.Add(btn);
            root.Add(btn2);

            return root;
        }

        private void Click_CodeGen()
        {
            RunDidChangeOnAssetType((ChangeProcessorAsset)_target);
        }

        public List<string> GetAssetPaths<T>() => GetAssetPaths(typeof(T).Name);

        public List<string> GetAssetPaths(string targetTypeName) =>
            AssetDatabase.FindAssets($"t:{targetTypeName}")
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .ToList();

        public List<ChangeProcessorAsset> GetChangeProcessorAssets(Type targetType)
        {
            var assetPaths = GetAssetPaths(targetType.Name);

            var changeProcessorAssets = new List<ChangeProcessorAsset>();

            foreach (var assetPath in assetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, targetType);

                if (asset == null || asset is not ChangeProcessorAsset)
                {
                    continue;
                }

                changeProcessorAssets.Add((ChangeProcessorAsset)asset);
            }

            return changeProcessorAssets;
        }

        public static void RunDidChangeOnAssetType(ChangeProcessorAsset target)
        {
            var targetType = target.GetType();
            Debug.Log($"Updating {targetType.Name} ...");

            var assets = AssetDatabaseUtility.GetSubAssets(targetType);

            var allAssets = new List<ChangeProcessorAsset>();

            foreach (var asset in assets)
            {
                if (asset == null || asset is not ChangeProcessorAsset changeProcessorAsset)
                {
                    continue;
                }

                allAssets.Add(changeProcessorAsset);
            }

            target.ProcessChanges(allAssets);

            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);
        }

        public static void Click_CodeGenAll()
        {
            var assets = AssetDatabaseUtility.GetSubAssets(typeof(ChangeProcessorAsset));

            var collector = new Dictionary<Type, List<ChangeProcessorAsset>>();

            foreach (var asset in assets)
            {
                if (asset == null || asset is not ChangeProcessorAsset changeProcessorAsset)
                {
                    continue;
                }

                var type = asset.GetType();

                if (!collector.TryGetValue(type, out var list))
                {
                    list = new List<ChangeProcessorAsset>();
                    collector[type] = list;
                }

                list.Add(changeProcessorAsset);
            }

            foreach (var entry in collector)
            {
                if (entry.Value.Count > 0)
                {
                    Debug.Log($"Updating {entry.Value[0].GetType()} ...");
                    entry.Value[0].ProcessChanges(entry.Value);
                }
            }

            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);
        }
    }
}