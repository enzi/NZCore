// <copyright project="NZCore.Editor" file="ChangeProcessorEditor.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using NZCore.AssetManagement;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace NZCore.Editor
{
    [CustomEditor(typeof(ScriptableObject), true, isFallback = true)]
    [CanEditMultipleObjects]
    public class ChangeProcessorEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            serializedObject.FillDefaultInspector(root, true);

            return new ChangeProcessorEditorElement(target).CreateInspectorGUI(root);
        }
    }

    public class ChangeProcessorEditorElement
    {
        private readonly Object _target;

        public ChangeProcessorEditorElement(Object target)
        {
            _target = target;
        }

        public VisualElement CreateInspectorGUI(VisualElement root)
        {
            if (_target is not IChangeProcessor targetAsset)
            {
                return root;
            }

            var hasChangesResult = targetAsset.HasChanges(GetChangeProcessorAssets(targetAsset.ProcessGroupType));
            if (hasChangesResult == HasChangeResult.None)
            {
                return root;
            }

            var changedSuffix = hasChangesResult == HasChangeResult.HasChanges ? " (*)" : string.Empty;
            root.Add(new Button(() => RunDidChangeOnAssetType(targetAsset))
            {
                text = $"Update {_target.GetType().Name} settings JSON{changedSuffix}"
            });
            root.Add(new Button(Click_CodeGenAll)
            {
                text = $"Update every JSON setting{changedSuffix}"
            });

            return root;
        }

        private static List<IChangeProcessor> GetChangeProcessorAssets(Type targetType)
        {
            return AssetDatabaseUtility.GetSubAssets(targetType)
                                       .OfType<IChangeProcessor>()
                                       .ToList();
        }

        public static void RunDidChangeOnAssetType(IChangeProcessor target)
        {
            var targetType = target.ProcessGroupType;
            Debug.Log($"Updating {targetType.Name} ...");

            var assets = AssetDatabaseUtility.GetSubAssets(targetType);
            var allAssets = assets.OfType<IChangeProcessor>().ToList();

            target.ProcessChanges(allAssets);

            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);
        }

        [MenuItem("Tools/Update every JSON setting")]
        public static void Click_CodeGenAll()
        {
            var processorTypes = TypeCache.GetTypesDerivedFrom<IChangeProcessor>()
                                          .Where(t => !t.IsAbstract && typeof(ScriptableObject).IsAssignableFrom(t));

            var assets = processorTypes.SelectMany(AssetDatabaseUtility.GetSubAssets)
                                       .OfType<IChangeProcessor>()
                                       .Distinct();
            var collector = new Dictionary<Type, List<IChangeProcessor>>();

            foreach (var asset in assets)
            {
                var type = asset.ProcessGroupType;

                if (!collector.TryGetValue(type, out var list))
                {
                    list = new List<IChangeProcessor>();
                    collector[type] = list;
                }

                list.Add(asset);
            }

            foreach (var entry in collector)
            {
                if (entry.Value.Count > 0)
                {
                    Debug.Log($"Updating {entry.Key} ...");
                    entry.Value[0].ProcessChanges(entry.Value);
                }
            }

            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);
        }
    }
}
