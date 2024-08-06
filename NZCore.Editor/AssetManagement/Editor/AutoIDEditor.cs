using System;
using System.Collections.Generic;
using System.Linq;
using NZCore.AssetManagement;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.Editor
{
    [CustomEditor(typeof(ScriptableObjectWithAutoID), true)]
    [CanEditMultipleObjects]
    public class AutoIDEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            serializedObject.FillDefaultInspector(root, true);

            Button btn = new Button(Click_CodeGen)
            {
                text = $"Update {target.GetType().Name}"
            };

            Button btn2 = new Button(Click_CodeGenAll)
            {
                text = $"Update all"
            };

            root.Add(btn);
            root.Add(btn2);

            return root;
        }

        private void Click_CodeGen()
        {
            RunDidChangeOnAssetType((ScriptableObjectWithAutoID)target);
        }

        private void Click_CodeGenAll()
        {
            var assetPaths = AssetDatabase.FindAssets($"t:ScriptableObjectWithAutoID")
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();

            Dictionary<Type, List<ScriptableObjectWithAutoID>> collector = new Dictionary<Type, List<ScriptableObjectWithAutoID>>();

            foreach (string assetPath in assetPaths)
            {
                var asset = (ScriptableObjectWithAutoID)AssetDatabase.LoadAssetAtPath(assetPath, typeof(ScriptableObjectWithAutoID));

                if (asset == null)
                    continue;

                var type = asset.GetType();

                if (!collector.TryGetValue(type, out var list))
                {
                    list = new List<ScriptableObjectWithAutoID>();
                    collector[type] = list;
                }

                list.Add(asset);
            }

            foreach (KeyValuePair<Type, List<ScriptableObjectWithAutoID>> entry in collector)
            {
                if (entry.Value.Count > 0)
                {
                    entry.Value[0].ChangeProcessor.DidChange(entry.Value);
                }
            }

            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);
        }

        public static void RunDidChangeOnAssetType(ScriptableObjectWithAutoID target)
        {
            var targetType = target.GetType();
            Debug.Log($"Generating code for {targetType.Name} ...");

            var assetPaths = AssetDatabase.FindAssets($"t:{targetType.Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();

            List<ScriptableObjectWithAutoID> allAssets = new List<ScriptableObjectWithAutoID>();

            foreach (string assetPath in assetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, targetType);

                if (asset == null)
                    continue;

                allAssets.Add((ScriptableObjectWithAutoID)asset);
            }

            target.ChangeProcessor.DidChange(allAssets);

            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);

            Debug.Log($"{targetType.Name} enum was created!");
        }
    }
}