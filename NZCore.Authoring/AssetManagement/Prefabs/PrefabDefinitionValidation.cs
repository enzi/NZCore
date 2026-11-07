// <copyright project="NZCore.Authoring" file="PrefabDefinitionValidation.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NZCore.AssetManagement
{
    public static class PrefabDefinitionValidation
    {
        public static List<PrefabDefinitionProblem> FindProblems()
        {
            var problems = new List<PrefabDefinitionProblem>();
            var definitions = PrefabDefinition.FindAll();
            var reservedIds = PrefabDefinitionChecks.CollectReservedIds(definitions, problems);
            var migrations = PrefabDefinitionChecks.ResolveMigrations(definitions, PrefabMigrationAsset.FindAll(), problems);

            foreach (var definition in definitions)
            {
                CheckDefinition(definition, reservedIds, migrations, problems);
            }

            CheckAuthorings(definitions, problems);
            return problems;
        }

        private static void CheckDefinition(PrefabDefinition definition, Dictionary<PrefabId, List<(PrefabDefinition Definition, string Key)>> reservedIds,
            Dictionary<PrefabId, PrefabId> migrations, List<PrefabDefinitionProblem> problems)
        {
            if (definition.Group != null && definition.Group.BoundType.Type != null)
            {
                problems.Add(new PrefabDefinitionProblem(
                    $"{definition.name} is in PrefabGroup {definition.Group.name}, which is bound to {definition.Group.BoundType.Type.Name}, so it can't hold definitions.",
                    definition));
            }

            foreach (var formerId in definition.FormerIds)
            {
                if (reservedIds.TryGetValue(formerId.Id, out var holders))
                {
                    problems.Add(new PrefabDefinitionProblem(
                        $"{formerId.Id} is a former ID of {definition.name}, but {PrefabDefinitionChecks.HolderNames(holders)} reserves it now. " +
                        "Old saves can't tell the two prefabs apart.", definition));
                }
            }

            if (definition.Retired)
            {
                var unmigrated = definition.ReservedIds.Where(reservedId => reservedId.Id != 0)
                                           .Select(reservedId => new PrefabId(definition.GroupId, reservedId.Id))
                                           .Where(prefabId => !migrations.ContainsKey(prefabId))
                                           .ToList();

                if (unmigrated.Count > 0)
                {
                    problems.Add(new PrefabDefinitionProblem(
                        $"{definition.name} is retired, but has no migration for {string.Join(", ", unmigrated)}, so old saves of it find no prefab.",
                        definition));
                }

                return;
            }

            if (definition.Target == null)
            {
                problems.Add(new PrefabDefinitionProblem($"{definition.name} has no prefab.", definition));
            }
            else if (definition.Target is GameObject prefab &&
                     (!prefab.TryGetComponent<PrefabDefinitionAuthoring>(out var authoring) || authoring.Definition != definition))
            {
                problems.Add(new PrefabDefinitionProblem(
                    $"{definition.name}'s prefab {prefab.name} has no {nameof(PrefabDefinitionAuthoring)} that points back at it, so its instances carry no PrefabId.",
                    definition));
            }
        }

        private static void CheckAuthorings(List<PrefabDefinition> definitions, List<PrefabDefinitionProblem> problems)
        {
            var definitionPaths = new HashSet<string>(definitions.Select(AssetDatabase.GetAssetPath));

            if (definitionPaths.Count == 0)
            {
                return;
            }

            foreach (var path in AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath).Distinct())
            {
                if (!AssetDatabase.GetDependencies(path, true).Any(definitionPaths.Contains))
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null || !prefab.TryGetComponent<PrefabDefinitionAuthoring>(out var authoring) || authoring.Definition == null ||
                    authoring.Definition.Target == prefab)
                {
                    continue;
                }

                var definition = authoring.Definition;
                var owned = definition.Target != null ? $"its prefab is {definition.Target.name}" : "it has no prefab";

                problems.Add(new PrefabDefinitionProblem(
                    $"{prefab.name} has a {nameof(PrefabDefinitionAuthoring)} that points at {definition.name}, but {owned}.",
                    prefab));
            }
        }
    }
}