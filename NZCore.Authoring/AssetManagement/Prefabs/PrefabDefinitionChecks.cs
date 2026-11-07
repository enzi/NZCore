// <copyright project="NZCore.Authoring" file="PrefabDefinitionChecks.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Object = UnityEngine.Object;

namespace NZCore.AssetManagement
{
    public readonly struct PrefabDefinitionProblem
    {
        public readonly string Message;
        public readonly Object Asset;

        public PrefabDefinitionProblem(string message, Object asset)
        {
            Message = message;
            Asset = asset;
        }
    }

    internal static class PrefabDefinitionChecks
    {
        public static Dictionary<PrefabId, List<(PrefabDefinition Definition, string Key)>> CollectReservedIds(List<PrefabDefinition> definitions,
            List<PrefabDefinitionProblem> problems)
        {
            var reservedIds = new Dictionary<PrefabId, List<(PrefabDefinition Definition, string Key)>>();

            foreach (var definition in definitions)
            {
                foreach (var reservedId in definition.ReservedIds)
                {
                    if (reservedId.Id == 0)
                    {
                        continue;
                    }

                    var prefabId = new PrefabId(definition.GroupId, reservedId.Id);

                    if (!reservedIds.TryGetValue(prefabId, out var holders))
                    {
                        holders = reservedIds[prefabId] = new List<(PrefabDefinition Definition, string Key)>();
                    }

                    holders.Add((definition, reservedId.Key));
                }
            }

            foreach (var reservedId in reservedIds.Where(reservedId => reservedId.Value.Count > 1))
            {
                problems.Add(new PrefabDefinitionProblem(
                    $"{reservedId.Key} is reserved by more than one PrefabDefinition: {HolderNames(reservedId.Value)}. " +
                    "The prefab database leaves it out until only one reserves it.", reservedId.Value[0].Definition));
            }

            return reservedIds;
        }

        public static string HolderNames(IEnumerable<(PrefabDefinition Definition, string Key)> holders) =>
            string.Join(", ", holders.Select(holder => $"{holder.Definition.name} ({PrefabDefinitionAssetPostprocessor.KeyName(holder.Key)})"));

        public static Dictionary<PrefabId, PrefabId> ResolveMigrations(List<PrefabDefinition> definitions, List<PrefabMigrationAsset> migrationAssets,
            List<PrefabDefinitionProblem> problems)
        {
            var migrations = new List<(PrefabId From, PrefabId To, Object Source)>();

            foreach (var definition in definitions)
            {
                foreach (var formerId in definition.FormerIds)
                {
                    if (definition.TryGetPrefabId(formerId.Key, out var currentId))
                    {
                        migrations.Add((formerId.Id, currentId, definition));
                    }
                    else
                    {
                        problems.Add(new PrefabDefinitionProblem(
                            $"{definition.name} has no current ID for {PrefabDefinitionAssetPostprocessor.KeyName(formerId.Key)}, " +
                            $"so its former ID {formerId.Id} has no migration.", definition));
                    }
                }
            }

            foreach (var asset in migrationAssets)
            {
                foreach (var migration in asset.Migrations)
                {
                    if (migration.From.IsNull || migration.To.IsNull)
                    {
                        problems.Add(new PrefabDefinitionProblem(
                            $"{asset.name} has a migration from {migration.From} to {migration.To}, but both IDs must be set.",
                            asset));
                        continue;
                    }

                    migrations.Add((migration.From, migration.To, asset));
                }
            }

            var next = new Dictionary<PrefabId, (PrefabId To, Object Source)>();

            foreach (var sources in migrations.GroupBy(migration => migration.From))
            {
                var targets = sources.Select(migration => migration.To).Distinct().ToList();

                if (targets.Count > 1)
                {
                    var names = string.Join(", ", sources.Select(migration => $"{migration.To} ({migration.Source.name})"));

                    problems.Add(new PrefabDefinitionProblem(
                        $"PrefabMigration maps {sources.Key} to more than one ID: {names}. The prefab database leaves {sources.Key} out.",
                        sources.First().Source));
                    continue;
                }

                next.Add(sources.Key, (targets[0], sources.First().Source));
            }

            return FollowChains(next, problems);
        }

        private static Dictionary<PrefabId, PrefabId> FollowChains(Dictionary<PrefabId, (PrefabId To, Object Source)> next,
            List<PrefabDefinitionProblem> problems)
        {
            var final = new Dictionary<PrefabId, PrefabId>();
            var reportedCycles = new HashSet<PrefabId>();

            foreach (var from in next.Keys)
            {
                var chain = new List<PrefabId> { from };
                var current = next[from].To;

                while (!chain.Contains(current) && next.TryGetValue(current, out var following))
                {
                    chain.Add(current);
                    current = following.To;
                }

                if (!chain.Contains(current))
                {
                    final.Add(from, current);
                    continue;
                }

                if (reportedCycles.Contains(current))
                {
                    continue;
                }

                var cycle = chain.Skip(chain.IndexOf(current)).Append(current).ToList();
                reportedCycles.UnionWith(cycle);

                problems.Add(new PrefabDefinitionProblem(
                    $"PrefabMigration has a cycle: {string.Join(" -> ", cycle)}. The prefab database leaves out every ID that leads into it.",
                    next[current].Source));
            }

            return final;
        }
    }
}