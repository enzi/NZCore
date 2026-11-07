// <copyright project="NZCore" file="PrefabDatabaseSystem.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;

namespace NZCore.AssetManagement
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SceneSystemGroup))]
    public partial struct PrefabDatabaseSystem : ISystem
    {
        private EntityQuery _changedEntriesQuery;
        private EntityQuery _prefabQuery;
        private int _prefabOrderVersion;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _changedEntriesQuery = SystemAPI.QueryBuilder().WithAll<PrefabDefinitionEntry, PrefabDefinitionHashMap>().Build();
            _changedEntriesQuery.SetChangedVersionFilter(ComponentType.ReadOnly<PrefabDefinitionEntry>());

            _prefabQuery = SystemAPI.QueryBuilder()
                                    .WithAll<Prefab, PrefabId>()
                                    .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)
                                    .Build();

            state.RequireForUpdate<PrefabDefinitionHashMap>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var prefabOrderVersion = state.EntityManager.GetComponentOrderVersion<Prefab>();

            if (prefabOrderVersion == _prefabOrderVersion && _changedEntriesQuery.IsEmpty)
            {
                return;
            }

            _prefabOrderVersion = prefabOrderVersion;

            var database = SystemAPI.GetSingletonEntity<PrefabDefinitionHashMap>();
            var entries = state.EntityManager.GetBuffer<PrefabDefinitionEntry>(database, true);
            var definitions = state.EntityManager.GetBuffer<PrefabDefinitionHashMap>(database).AsHashMap<PrefabDefinitionHashMap, PrefabId, Entity>();

            definitions.Clear();

            foreach (var entry in entries)
            {
                definitions.TryAdd(entry.Id, entry.Prefab);
            }

            var prefabIds = _prefabQuery.ToComponentDataArray<PrefabId>(Allocator.Temp);
            var prefabs = _prefabQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < prefabIds.Length; i++)
            {
                if (!prefabIds[i].IsNull)
                {
                    definitions.TryAdd(prefabIds[i], prefabs[i]);
                }
            }
        }
    }
}