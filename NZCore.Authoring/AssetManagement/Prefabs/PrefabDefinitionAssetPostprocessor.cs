// <copyright project="NZCore.Authoring" file="PrefabDefinitionAssetPostprocessor.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace NZCore.AssetManagement
{
    public class PrefabDefinitionAssetPostprocessor : AssetPostprocessor
    {
        [UsedImplicitly]
        // ReSharper disable once Unity.IncorrectMethodSignature
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            if (didDomainReload)
            {
                return;
            }

            var affectsDefinitions = importedAssets.Any(CanAffectDefinitions);

            if (affectsDefinitions || deletedAssets.Length > 0)
            {
                PrefabDefinition.ClearOwnerIndex();
            }

            if (!affectsDefinitions)
            {
                return;
            }

            var imported = new HashSet<string>(importedAssets);
            var definitions = PrefabDefinition.FindAll();

            var changed = definitions.Where(definition => imported.Contains(AssetDatabase.GetAssetPath(definition)) ||
                                                          (definition.Target != null && imported.Contains(AssetDatabase.GetAssetPath(definition.Target))) ||
                                                          (definition.Group != null && imported.Contains(AssetDatabase.GetAssetPath(definition.Group))))
                                     .ToList();

            if (changed.Count == 0)
            {
                return;
            }

            var usedIdsByGroup = new Dictionary<int, HashSet<int>>();

            foreach (var definition in definitions)
            {
                var usedIds = GetUsedIds(usedIdsByGroup, definition.ReservedGroupId != 0 ? definition.ReservedGroupId : definition.GroupId);

                foreach (var reservedId in definition.ReservedIds)
                {
                    if (reservedId.Id != 0)
                    {
                        usedIds.Add(reservedId.Id);
                    }
                }

                foreach (var formerId in definition.FormerIds)
                {
                    GetUsedIds(usedIdsByGroup, formerId.Id.Group).Add(formerId.Id.Id);
                }
            }

            foreach (var definition in changed)
            {
                if (definition.Group != null && definition.Group.BoundType.Type != null)
                {
                    Debug.LogError($"{definition.name} is in PrefabGroup {definition.Group.name}, which is bound to {definition.Group.BoundType.Type.Name}. " +
                                   "A bound group takes its IDs from that type, so no PrefabDefinition can use it.", definition);
                    continue;
                }

                var usedIds = GetUsedIds(usedIdsByGroup, definition.GroupId);
                var moved = MoveToCurrentGroup(definition, usedIds);
                var allocated = Allocate(definition, usedIds);

                if (moved || allocated)
                {
                    EditorUtility.SetDirty(definition);
                    AssetDatabase.SaveAssetIfDirty(definition);
                }
            }

            ReportDuplicates(definitions, changed);
        }

        private static bool CanAffectDefinitions(string path)
        {
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);

            return type == typeof(PrefabDefinition) || type == typeof(PrefabGroup) || type == typeof(GameObject) ||
                   (type != null && typeof(IMultiPrefabSource).IsAssignableFrom(type));
        }

        private static bool MoveToCurrentGroup(PrefabDefinition definition, HashSet<int> usedIds)
        {
            if (definition.ReservedGroupId == definition.GroupId)
            {
                return false;
            }

            if (definition.ReservedGroupId != 0)
            {
                for (var i = 0; i < definition.ReservedIds.Count; i++)
                {
                    var reservedId = definition.ReservedIds[i];

                    if (reservedId.Id == 0)
                    {
                        continue;
                    }

                    definition.FormerIds.Add(new FormerPrefabId { Key = reservedId.Key, Id = new PrefabId(definition.ReservedGroupId, reservedId.Id) });

                    var id = GetFirstFreeId(usedIds);

                    if (id == 0)
                    {
                        Debug.LogError($"Prefab group {definition.GroupId} has run out of IDs, so {definition.name} gets no ID for {KeyName(reservedId.Key)}.",
                            definition);
                    }
                    else
                    {
                        usedIds.Add(id);
                    }

                    definition.ReservedIds[i] = new ReservedPrefabId { Key = reservedId.Key, Id = id };
                }
            }

            definition.ReservedGroupId = definition.GroupId;
            return true;
        }

        private static HashSet<int> GetUsedIds(Dictionary<int, HashSet<int>> usedIdsByGroup, int groupId)
        {
            if (!usedIdsByGroup.TryGetValue(groupId, out var usedIds))
            {
                usedIds = usedIdsByGroup[groupId] = new HashSet<int>();
            }

            return usedIds;
        }

        private static bool Allocate(PrefabDefinition definition, HashSet<int> usedIds)
        {
            var allocated = false;

            foreach (var key in definition.GetKeys())
            {
                var index = definition.ReservedIds.FindIndex(reservedId => reservedId.Key == key);

                if (index >= 0 && definition.ReservedIds[index].Id != 0)
                {
                    continue;
                }

                var id = GetFirstFreeId(usedIds);

                if (id == 0)
                {
                    Debug.LogError($"Prefab group {definition.GroupId} has run out of IDs, so {definition.name} gets no ID for {KeyName(key)}.", definition);
                    break;
                }

                usedIds.Add(id);

                var reservedId = new ReservedPrefabId { Key = key, Id = id };

                if (index >= 0)
                {
                    definition.ReservedIds[index] = reservedId;
                }
                else
                {
                    definition.ReservedIds.Add(reservedId);
                }

                allocated = true;
            }

            return allocated;
        }

        private static int GetFirstFreeId(HashSet<int> usedIds)
        {
            for (var id = 1; id <= PrefabId.MaxId; id++)
            {
                if (!usedIds.Contains(id))
                {
                    return id;
                }
            }

            return 0;
        }

        private static void ReportDuplicates(List<PrefabDefinition> definitions, List<PrefabDefinition> changed)
        {
            var duplicates = definitions.SelectMany(definition => definition.ReservedIds
                                                                            .Where(reservedId => reservedId.Id != 0)
                                                                            .Select(reservedId => (Definition: definition, reservedId.Key, reservedId.Id)))
                                        .GroupBy(entry => (entry.Definition.GroupId, entry.Id))
                                        .Where(holders => holders.Count() > 1 && holders.Any(entry => changed.Contains(entry.Definition)));

            foreach (var holders in duplicates)
            {
                var names = string.Join(", ", holders.Select(entry => $"{entry.Definition.name} ({KeyName(entry.Key)})"));

                Debug.LogError($"ID {holders.Key.Id} in prefab group {holders.Key.GroupId} is reserved more than once: {names}. " +
                               "Set one of the IDs to 0 to assign a new one.", holders.First(entry => changed.Contains(entry.Definition)).Definition);
            }
        }

        internal static string KeyName(string key) => key == PrefabDefinition.DefaultKey ? "default key" : $"key {key}";
    }
}