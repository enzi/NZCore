// <copyright project="NZCore" file="ChangeProcessorEditor.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
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
        public Object target;

        public ChangeProcessorEditorElement(Object target)
        {
            this.target = target;
        }
        
        public VisualElement CreateInspectorGUI(VisualElement root)
        {
            var hasChanges = ((ChangeProcessorAsset)target).HasChanges(GetChangeProcessorAssets(target.GetType()));

            Button btn = new Button(Click_CodeGen)
            {
                text = $"Update {target.GetType().Name} settings JSON {(hasChanges ? "(*)" : "")}"
            };

            Button btn2 = new Button(Click_CodeGenAll)
            {
                text = $"Update every JSON setting {(hasChanges ? "(*)" : "")}"
            };

            root.Add(btn);
            root.Add(btn2);

            return root;
        }
        
        private void Click_CodeGen()
        {
            RunDidChangeOnAssetType((ChangeProcessorAsset)target);
        }

        public List<string> GetAssetPaths<T>()
        {
            return GetAssetPaths(typeof(T).Name);
        }

        public List<string> GetAssetPaths(string targetTypeName)
        {
            return AssetDatabase.FindAssets($"t:{targetTypeName}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();
        }

        public List<ChangeProcessorAsset> GetChangeProcessorAssets(Type targetType)
        {
            var assetPaths = GetAssetPaths(targetType.Name);
            
            List<ChangeProcessorAsset> changeProcessorAssets = new List<ChangeProcessorAsset>();

            foreach (string assetPath in assetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, targetType);

                if (asset == null || asset is not ChangeProcessorAsset)
                    continue;

                changeProcessorAssets.Add((ChangeProcessorAsset)asset);
            }

            return changeProcessorAssets;
        }

        public static void RunDidChangeOnAssetType(ChangeProcessorAsset target)
        {
            var targetType = target.GetType();
            Debug.Log($"Updating {targetType.Name} ...");

            var assetPaths = AssetDatabase.FindAssets($"t:{targetType.Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();

            List<ChangeProcessorAsset> allAssets = new List<ChangeProcessorAsset>();

            foreach (string assetPath in assetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, targetType);

                if (asset == null)
                    continue;

                allAssets.Add((ChangeProcessorAsset)asset);
            }

            target.ProcessChanges(allAssets);

            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);
        }

        private void Click_CodeGenAll()
        {
            var assetPaths = AssetDatabase.FindAssets($"t:ScriptableObjectWithAutoID")
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();

            Dictionary<Type, List<ChangeProcessorAsset>> collector = new Dictionary<Type, List<ChangeProcessorAsset>>();

            foreach (string assetPath in assetPaths)
            {
                var asset = (ChangeProcessorAsset)AssetDatabase.LoadAssetAtPath(assetPath, typeof(ChangeProcessorAsset));

                if (asset == null)
                    continue;

                var type = asset.GetType();

                if (!collector.TryGetValue(type, out var list))
                {
                    list = new List<ChangeProcessorAsset>();
                    collector[type] = list;
                }

                list.Add(asset);
            }

            foreach (KeyValuePair<Type, List<ChangeProcessorAsset>> entry in collector)
            {
                if (entry.Value.Count > 0)
                {
                    entry.Value[0].ProcessChanges(entry.Value);
                }
            }

            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);
        }
    }
}