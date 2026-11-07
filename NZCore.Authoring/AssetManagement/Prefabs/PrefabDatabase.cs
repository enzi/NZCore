// <copyright project="NZCore.Authoring" file="PrefabDatabase.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace NZCore.AssetManagement
{
    [CreateAssetMenu(fileName = "PrefabDatabase", menuName = "NZCore/Create Prefab Database")]
    public class PrefabDatabase : ScriptableObjectDatabase<PrefabDatabase>
    {
        public override void Bake(IBaker baker, Entity entity)
        {
            Bake(baker, entity, PrefabDefinition.FindAll(), PrefabMigrationAsset.FindAll());
        }

        internal static void Bake(IBaker baker, Entity entity, List<PrefabDefinition> definitions, List<PrefabMigrationAsset> migrationAssets)
        {
            foreach (var definition in definitions)
            {
                baker.DependsOn(definition);
                baker.DependsOn(definition.Group);
            }

            foreach (var asset in migrationAssets)
            {
                baker.DependsOn(asset);
            }

            var problems = new List<PrefabDefinitionProblem>();
            var reservedIds = PrefabDefinitionChecks.CollectReservedIds(definitions, problems);
            var migrations = PrefabDefinitionChecks.ResolveMigrations(definitions, migrationAssets, problems);

            foreach (var problem in problems)
            {
                Debug.LogError(problem.Message, problem.Asset);
            }

            var prefabs = CollectPrefabs(baker, definitions, reservedIds);
            var entries = baker.AddBuffer<PrefabDefinitionEntry>(entity);

            foreach (var prefab in prefabs)
            {
                entries.Add(prefab);
            }

            DynamicHashMap<PrefabDefinitionHashMap, PrefabId, Entity>.Initialize(baker.AddBuffer<PrefabDefinitionHashMap>(entity), prefabs.Count);

            var migrationMap = baker.AddBuffer<PrefabMigrationHashMap>(entity).AsHashMap<PrefabMigrationHashMap, PrefabId, PrefabId>();

            foreach (var migration in migrations)
            {
                migrationMap.TryAdd(migration.Key, migration.Value);
            }
        }

        private static List<PrefabDefinitionEntry> CollectPrefabs(IBaker baker, List<PrefabDefinition> definitions,
            Dictionary<PrefabId, List<(PrefabDefinition Definition, string Key)>> reservedIds)
        {
            var entries = new List<PrefabDefinitionEntry>();

            foreach (var definition in definitions)
            {
                if (definition.Target is not GameObject prefab)
                {
                    continue;
                }

                var prefabId = PrefabIdBaking.FromDefinition(baker, definition);

                if (prefabId.IsNull || reservedIds[prefabId].Count > 1)
                {
                    continue;
                }

                entries.Add(new PrefabDefinitionEntry { Id = prefabId, Prefab = baker.GetEntity(prefab, TransformUsageFlags.Dynamic) });
            }

            return entries;
        }
    }
}