// <copyright project="NZCore.Authoring" file="PrefabGroupAssetPostprocessor.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
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

            ReportBindingProblems();
        }

        private static void ReportBindingProblems()
        {
            var boundGroups = PrefabGroup.FindAll().Where(group => group.BoundType.Type != null).ToList();

            foreach (var binding in boundGroups.GroupBy(group => group.Binding))
            {
                var groups = binding.ToList();

                if (groups.Count > 1)
                {
                    Debug.LogError($"{PrefabGroup.BindingName(binding.Key.Type, binding.Key.Key)} is bound to more than one PrefabGroup: " +
                                   $"{string.Join(", ", groups.Select(group => group.name))}", groups[0]);
                }
            }

            foreach (var binding in boundGroups.GroupBy(group => group.BoundType.Type))
            {
                var groups = binding.ToList();
                var renumbering = FindRenumberingType(binding.Key);

                if (renumbering != null)
                {
                    Debug.LogError(
                        $"{binding.Key.Name} is bound to PrefabGroup {string.Join(", ", groups.Select(group => group.name))}, but {renumbering.Name} " +
                        "renumbers duplicate AutoIDs, which changes PrefabIds in save files without a report. " +
                        $"Override {nameof(ScriptableObjectWithAutoID.ConflictMode)} to return {nameof(AutoIDConflictMode.Error)}.", groups[0]);
                }
            }
        }

        private static Type FindRenumberingType(Type boundType)
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom(boundType).Append(boundType))
            {
                if (type.IsAbstract || type.ContainsGenericParameters)
                {
                    continue;
                }

                var instance = (ScriptableObjectWithAutoID)ScriptableObject.CreateInstance(type);
                var conflictMode = instance.ConflictMode;
                UnityEngine.Object.DestroyImmediate(instance);

                if (conflictMode == AutoIDConflictMode.Renumber)
                {
                    return type;
                }
            }

            return null;
        }
    }
}