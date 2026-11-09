// <copyright project="NZCore.Authoring" file="PrefabDefinitionTooling.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NZCore.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NZCore.AssetManagement
{
    public static class PrefabDefinitionTooling
    {
        public const string RetiredFolderName = "Retired";
        public const string RetiredPrefix = "Retired_";

        public static List<PrefabDefinition> CreateDefinitions(IEnumerable<GameObject> prefabs, PrefabGroup group, string folder)
        {
            var selected = prefabs.ToList();
            var selectedSet = new HashSet<GameObject>(selected);
            var owners = FindOwners();
            var takenPaths = new HashSet<string>();
            var created = new List<PrefabDefinition>();

            AssetDatabase.StartAssetEditing();

            try
            {
                foreach (var prefab in selected.Concat(FindVariants(selected)).Distinct())
                {
                    if (!CanHoldAuthoring(prefab, out var error))
                    {
                        Debug.LogWarning(error, prefab);
                        continue;
                    }

                    if (owners.TryGetValue(prefab, out var owner))
                    {
                        if (selectedSet.Contains(prefab))
                        {
                            Debug.LogWarning($"{prefab.name} already belongs to {owner.name}, so it gets no new definition.", prefab);
                        }

                        continue;
                    }

                    var definition = ScriptableObject.CreateInstance<PrefabDefinition>();
                    definition.Group = group;
                    definition.Target = prefab;
                    AssetDatabase.CreateAsset(definition, UniquePath(folder, $"{prefab.name}Definition", takenPaths));
                    created.Add(definition);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            foreach (var depth in created.GroupBy(definition => VariantDepth((GameObject)definition.Target)).OrderBy(depth => depth.Key))
            {
                AssetDatabase.StartAssetEditing();

                try
                {
                    foreach (var definition in depth)
                    {
                        SetAuthoring((GameObject)definition.Target, definition);
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
            }

            return created;
        }

        public static bool TryAssignTarget(PrefabDefinition definition, Object target, out string error)
        {
            error = null;

            if (target == definition.Target)
            {
                return true;
            }

            if (target is GameObject prefab)
            {
                if (!CanHoldAuthoring(prefab, out error))
                {
                    return false;
                }

                if (FindOwners().TryGetValue(prefab, out var owner))
                {
                    error = $"{prefab.name} already belongs to {owner.name}.";
                    return false;
                }
            }
            else if (target != null && target is not IMultiPrefabSource)
            {
                error = $"{target.name} is neither a prefab nor a ScriptableObject that implements {nameof(IMultiPrefabSource)}.";
                return false;
            }

            var previous = definition.Target;
            definition.Target = target;
            Save(definition);

            if (previous is GameObject previousPrefab)
            {
                RemoveAuthoring(previousPrefab, definition);
            }

            if (target is GameObject newPrefab)
            {
                SetAuthoring(newPrefab, definition);
            }

            return true;
        }

        public static bool TryRetire(PrefabDefinition definition, PrefabDefinition replacement, PrefabMigrationAsset migrationAsset, out string error)
        {
            error = null;

            if (definition.Retired)
            {
                error = $"{definition.name} is already retired.";
                return false;
            }

            if (replacement == definition)
            {
                error = $"{definition.name} can't replace itself.";
                return false;
            }

            if (replacement != null)
            {
                AddMigrations(definition, replacement, migrationAsset);
            }

            var previous = definition.Target;
            definition.Target = null;
            definition.Retired = true;
            Save(definition);

            if (previous is GameObject prefab)
            {
                RemoveAuthoring(prefab, definition);
            }

            var path = AssetDatabase.GetAssetPath(definition);
            var folder = Path.GetDirectoryName(path)!.Replace('\\', '/');
            var retiredFolder = $"{folder}/{RetiredFolderName}";

            if (!AssetDatabase.IsValidFolder(retiredFolder))
            {
                AssetDatabase.CreateFolder(folder, RetiredFolderName);
            }

            error = AssetDatabase.MoveAsset(path, AssetDatabase.GenerateUniqueAssetPath($"{retiredFolder}/{RetiredPrefix}{definition.name}.asset"));
            return string.IsNullOrEmpty(error);
        }

        public static PrefabMigrationAsset FindOrCreateMigrationAsset()
        {
            var asset = PrefabMigrationAsset.FindAll().FirstOrDefault();

            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<PrefabMigrationAsset>();
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{SettingsOverviewWindow.SettingsRoot}/PrefabMigrations.asset"));
            return asset;
        }

        private static void AddMigrations(PrefabDefinition definition, PrefabDefinition replacement, PrefabMigrationAsset migrationAsset)
        {
            foreach (var reservedId in definition.ReservedIds)
            {
                if (reservedId.Id == 0)
                {
                    continue;
                }

                var from = new PrefabId(definition.GroupId, reservedId.Id);

                if (replacement.TryGetPrefabId(reservedId.Key, out var to))
                {
                    migrationAsset.Migrations.Add(new PrefabMigrationEntry { From = from, To = to });
                }
                else
                {
                    Debug.LogWarning($"{replacement.name} has no ID for {PrefabDefinitionAssetPostprocessor.KeyName(reservedId.Key)}, " +
                                     $"so {from} of {definition.name} gets no migration.", replacement);
                }
            }

            Save(migrationAsset);
        }

        private static bool CanHoldAuthoring(GameObject prefab, out string error)
        {
            error = null;

            if (!PrefabUtility.IsPartOfPrefabAsset(prefab) || prefab.transform.parent != null)
            {
                error = $"{prefab.name} isn't the root of a prefab asset.";
            }
            else if (PrefabUtility.GetPrefabAssetType(prefab) is not (PrefabAssetType.Regular or PrefabAssetType.Variant))
            {
                error = $"{prefab.name} is a {PrefabUtility.GetPrefabAssetType(prefab)} prefab, which can't hold a {nameof(PrefabDefinitionAuthoring)}.";
            }
            else if (PrefabUtility.IsPartOfImmutablePrefab(prefab))
            {
                error = $"{prefab.name} is read-only, so it can't hold a {nameof(PrefabDefinitionAuthoring)}.";
            }

            return error == null;
        }

        public static Dictionary<GameObject, PrefabDefinition> FindOwners()
        {
            var owners = new Dictionary<GameObject, PrefabDefinition>();

            foreach (var definition in PrefabDefinition.FindAll())
            {
                if (definition.Target is GameObject prefab)
                {
                    owners.TryAdd(prefab, definition);
                }
            }

            return owners;
        }

        private static IEnumerable<GameObject> FindVariants(List<GameObject> bases)
        {
            var basePaths = new HashSet<string>(bases.Select(AssetDatabase.GetAssetPath));
            var baseSet = new HashSet<GameObject>(bases);

            foreach (var path in AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath).Distinct())
            {
                if (basePaths.Contains(path) || !AssetDatabase.GetDependencies(path, true).Any(basePaths.Contains))
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Variant)
                {
                    continue;
                }

                for (var source = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
                     source != null;
                     source = PrefabUtility.GetCorrespondingObjectFromSource(source))
                {
                    if (baseSet.Contains(source))
                    {
                        yield return prefab;
                        break;
                    }
                }
            }
        }

        private static int VariantDepth(GameObject prefab)
        {
            var depth = 0;

            for (var source = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
                 source != null;
                 source = PrefabUtility.GetCorrespondingObjectFromSource(source))
            {
                depth++;
            }

            return depth;
        }

        private static void SetAuthoring(GameObject prefab, PrefabDefinition definition)
        {
            if (prefab.TryGetComponent<PrefabDefinitionAuthoring>(out var existing) && existing.Definition == definition)
            {
                return;
            }

            using var scope = new PrefabUtility.EditPrefabContentsScope(AssetDatabase.GetAssetPath(prefab));
            var root = scope.prefabContentsRoot;

            if (!root.TryGetComponent<PrefabDefinitionAuthoring>(out var authoring))
            {
                authoring = root.AddComponent<PrefabDefinitionAuthoring>();
            }

            authoring.Definition = definition;
        }

        private static void RemoveAuthoring(GameObject prefab, PrefabDefinition definition)
        {
            if (prefab == null || !prefab.TryGetComponent<PrefabDefinitionAuthoring>(out var authoring) || authoring.Definition != definition)
            {
                return;
            }

            using var scope = new PrefabUtility.EditPrefabContentsScope(AssetDatabase.GetAssetPath(prefab));
            Object.DestroyImmediate(scope.prefabContentsRoot.GetComponent<PrefabDefinitionAuthoring>());
        }

        private static string UniquePath(string folder, string name, HashSet<string> takenPaths)
        {
            var path = $"{folder}/{name}.asset";

            for (var i = 1; takenPaths.Contains(path) || File.Exists(path); i++)
            {
                path = $"{folder}/{name} {i}.asset";
            }

            takenPaths.Add(path);
            return path;
        }

        private static void Save(Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}