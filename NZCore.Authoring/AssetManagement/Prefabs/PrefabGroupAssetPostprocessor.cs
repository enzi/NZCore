// <copyright project="NZCore.Authoring" file="PrefabGroupAssetPostprocessor.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace NZCore.AssetManagement
{
    public class PrefabGroupAssetPostprocessor : AssetPostprocessor
    {
        [UsedImplicitly]
        // ReSharper disable once Unity.IncorrectMethodSignature
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            if (didDomainReload || importedAssets.All(path => AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(PrefabGroup)))
            {
                return;
            }

            ReportTypesBoundToSeveralGroups();
        }

        private static void ReportTypesBoundToSeveralGroups()
        {
            var boundGroups = AssetDatabase.FindAssets($"t:{nameof(PrefabGroup)}")
                                           .Select(AssetDatabase.GUIDToAssetPath)
                                           .Distinct()
                                           .Select(AssetDatabase.LoadAssetAtPath<PrefabGroup>)
                                           .Where(group => group != null && group.BoundType.Type != null);

            foreach (var binding in boundGroups.GroupBy(group => group.BoundType.Type))
            {
                var groups = binding.ToList();

                if (groups.Count > 1)
                {
                    Debug.LogError($"{binding.Key.Name} is bound to more than one PrefabGroup: {string.Join(", ", groups.Select(group => group.name))}",
                        groups[0]);
                }
            }
        }
    }
}