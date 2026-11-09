// <copyright project="NZCore.Tests" file="PrefabDatabaseTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NZCore.AssetManagement;
using NZCore.Tests.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NZCore.Tests.AssetManagement
{
    public class PrefabDatabaseTests : EcsTestsFixture
    {
        private string _folder;
        private PrefabGroup _group;
        private GameObject _baked;
        private BlobAssetStore _blobAssetStore;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            _blobAssetStore = new BlobAssetStore(128);
            _folder = TestAssets.CreateFolder("PrefabDatabaseTestsTemp");
            _group = TestAssets.Create<PrefabGroup>(_folder, "Group");
        }

        [TearDown]
        public override void TearDown()
        {
            Object.DestroyImmediate(_baked);
            AssetDatabase.DeleteAsset(_folder);
            _blobAssetStore.Dispose();
            base.TearDown();
        }

        [Test]
        public void SettingsSubScene_ResolvesTheDefinitionsPrefabAfterLoadingBecauseBakedEntityReferencesAreRemapped()
        {
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World,
                DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default)
                                          .Where(type => type.Assembly.GetName().Name is "Unity.Entities" or "Unity.Entities.Hybrid" or "Unity.Scenes"));

            var prefabId = CreateDefinitionWithPrefab("Root", out _);
            var scenePath = $"{_folder}/Settings.unity";
            var scene = TestSubScenes.CreateAndOpen(scenePath);
            var database = new GameObject("Database");
            database.AddComponent<PrefabDatabaseTestAuthoring>().Folder = _folder;
            SceneManager.MoveGameObjectToScene(database, scene);
            TestSubScenes.SaveAndClose(scene);

            TestSubScenes.Load(World, scenePath);
            UpdateDatabaseSystem();

            var prefab = Definitions()[prefabId];
            Assert.IsTrue(Manager.Exists(prefab) && Manager.HasComponent<Prefab>(prefab),
                "The map must point at the loaded prefab, not at a baking-world entity");
            Assert.AreEqual(prefabId, Manager.GetComponentData<PrefabId>(prefab));
        }

        [Test]
        public void IdReservedByTwoDefinitions_IsAnErrorAndResolvesToNeitherPrefabSoASaveCantLoadTheWrongOne()
        {
            var duplicateId = CreateDefinitionWithPrefab("First", out _);
            CreateDefinitionWithPrefab("Second", out var second);

            second.ReservedIds[0] = new ReservedPrefabId { Key = PrefabDefinition.DefaultKey, Id = duplicateId.Id };

            LogAssert.Expect(LogType.Error, new Regex($"^{Regex.Escape(duplicateId.ToString())} is reserved by more than one PrefabDefinition: "));

            BakeDatabase();
            UpdateDatabaseSystem();

            Assert.IsFalse(Definitions().ContainsKey(duplicateId));
        }

        [Test]
        public void DatabaseSystem_UpdatesBeforeSavingSoASectionRestoredInTheFrameItsSubSceneLoadsFindsThePrefabsThatSubSceneBuilds()
        {
            var initialization = World.GetOrCreateSystemManaged<InitializationSystemGroup>();
            var saving = World.GetOrCreateSystemManaged<SavingSystemGroup>().SystemHandle;
            var database = World.GetOrCreateSystem<PrefabDatabaseSystem>();

            initialization.AddSystemToUpdateList(World.GetOrCreateSystemManaged<SceneSystemGroup>());
            initialization.AddSystemToUpdateList(saving);
            initialization.AddSystemToUpdateList(database);
            initialization.SortSystems();

            var order = initialization.GetAllSystems(Allocator.Temp);
            Assert.Less(order.IndexOf(database), order.IndexOf(saving));
        }

        [Test]
        public void MigrationChain_MapsEveryOldIdToTheLastIdAcrossTheMigrationAssetAndFormerIds()
        {
            var currentId = CreateDefinition("Moved", out var definition);
            var formerId = Id(9001);
            definition.FormerIds.Add(new FormerPrefabId { Key = PrefabDefinition.DefaultKey, Id = formerId });
            TestAssets.Save(definition);

            CreateMigrations((Id(9002), Id(9003)), (Id(9003), formerId));

            BakeDatabase();

            var migrations = Migrations();
            Assert.AreEqual(currentId, migrations[formerId]);
            Assert.AreEqual(currentId, migrations[Id(9003)]);
            Assert.AreEqual(currentId, migrations[Id(9002)], "A save that is several migrations old must still reach the current ID in one lookup");
        }

        [Test]
        public void MigrationCycle_IsReportedOnceAndLeavesOutEveryIdThatLeadsIntoIt()
        {
            CreateMigrations((Id(9001), Id(9002)), (Id(9002), Id(9001)), (Id(9003), Id(9001)), (Id(9004), Id(9005)));

            LogAssert.Expect(LogType.Error, new Regex("^PrefabMigration has a cycle: "));

            BakeDatabase();

            var migrations = Migrations();
            Assert.IsFalse(migrations.ContainsKey(Id(9001)));
            Assert.IsFalse(migrations.ContainsKey(Id(9002)));
            Assert.IsFalse(migrations.ContainsKey(Id(9003)));
            Assert.AreEqual(Id(9005), migrations[Id(9004)], "A cycle must not stop unrelated migrations from baking");
        }

        [Test]
        public void OldIdMappedToTwoTargets_IsAnErrorAndLeftOutEvenWhenTheTargetsComeFromDifferentSources()
        {
            CreateDefinition("Moved", out var definition);
            var formerId = Id(9001);
            definition.FormerIds.Add(new FormerPrefabId { Key = PrefabDefinition.DefaultKey, Id = formerId });
            TestAssets.Save(definition);

            CreateMigrations((formerId, Id(9002)));

            LogAssert.Expect(LogType.Error, new Regex($"^PrefabMigration maps {Regex.Escape(formerId.ToString())} to more than one ID: "));

            BakeDatabase();

            Assert.IsFalse(Migrations().ContainsKey(formerId));
        }

        [Test]
        public void LoadedPrefab_IsRegisteredAndRemovedWhenItsSubSceneUnloads()
        {
            CreateDatabase();
            var prefabId = Id(1);
            var prefab = CreatePrefab(prefabId);
            var instance = Manager.CreateEntity();
            Manager.AddComponentData(instance, Id(2));

            UpdateDatabaseSystem();

            Assert.AreEqual(prefab, Definitions()[prefabId]);
            Assert.IsFalse(Definitions().ContainsKey(Id(2)), "An instance must not register, only a prefab can be spawned from the map");

            Manager.DestroyEntity(prefab);
            UpdateDatabaseSystem();

            Assert.IsFalse(Definitions().ContainsKey(prefabId));
        }

        [Test]
        public void CopyOfARegisteredPrefab_TakesOverWhenTheRegisteredCopyUnloads()
        {
            CreateDatabase();
            var prefabId = Id(1);
            var first = CreatePrefab(prefabId);
            var second = CreatePrefab(prefabId);

            UpdateDatabaseSystem();

            var registered = Definitions()[prefabId];
            Assert.That(registered, Is.EqualTo(first).Or.EqualTo(second));

            Manager.DestroyEntity(registered);
            UpdateDatabaseSystem();

            Assert.AreEqual(registered == first ? second : first, Definitions()[prefabId]);
        }

        [Test]
        public void BakedEntry_WinsOverALoadedCopyWithTheSameId()
        {
            var prefabId = Id(1);
            var copy = CreatePrefab(prefabId);
            var baked = CreatePrefab(prefabId);
            CreateDatabase(new PrefabDefinitionEntry { Id = prefabId, Prefab = baked });

            UpdateDatabaseSystem();

            Assert.AreEqual(baked, Definitions()[prefabId]);
        }

        [Test]
        public void ReloadedSettingsSubScene_RebuildsTheMapEvenWhenNoPrefabChanged()
        {
            var target = Manager.CreateEntity();
            var oldDatabase = CreateDatabase(new PrefabDefinitionEntry { Id = Id(1), Prefab = target });
            UpdateDatabaseSystem();

            Manager.DestroyEntity(oldDatabase);
            CreateDatabase(new PrefabDefinitionEntry { Id = Id(2), Prefab = target });
            UpdateDatabaseSystem();

            Assert.AreEqual(target, Definitions()[Id(2)]);
        }

        private PrefabId Id(int id) => new(_group.AutoID, id);

        private PrefabId CreateDefinition(string name, out PrefabDefinition definition)
        {
            definition = TestAssets.Create<PrefabDefinition>(_folder, name, created => created.Group = _group);
            Assert.IsTrue(definition.TryGetPrefabId(PrefabDefinition.DefaultKey, out var prefabId));
            return prefabId;
        }

        private PrefabId CreateDefinitionWithPrefab(string name, out PrefabDefinition definition)
        {
            var prefabId = CreateDefinition(name, out definition);

            var root = new GameObject(name);
            root.AddComponent<PrefabDefinitionAuthoring>().Definition = definition;
            definition.Target = TestAssets.CreatePrefab(_folder, root);
            TestAssets.Save(definition);

            return prefabId;
        }

        private void CreateMigrations(params (PrefabId From, PrefabId To)[] migrations)
        {
            TestAssets.Create<PrefabMigrationAsset>(_folder, "Migrations", asset =>
                asset.Migrations = migrations.Select(migration => new PrefabMigrationEntry { From = migration.From, To = migration.To }).ToList());
        }

        private Entity CreateDatabase(params PrefabDefinitionEntry[] entries)
        {
            var database = Manager.CreateEntity();
            Manager.AddBuffer<PrefabDefinitionEntry>(database).AddRange(new NativeArray<PrefabDefinitionEntry>(entries, Allocator.Temp));
            DynamicHashMap<PrefabDefinitionHashMap, PrefabId, Entity>.Initialize(Manager.AddBuffer<PrefabDefinitionHashMap>(database), entries.Length);
            return database;
        }

        private Entity CreatePrefab(PrefabId prefabId)
        {
            var prefab = Manager.CreateEntity(typeof(Prefab));
            Manager.AddComponentData(prefab, prefabId);
            return prefab;
        }

        private void BakeDatabase()
        {
            Object.DestroyImmediate(_baked);
            _baked = new GameObject("Database");
            _baked.AddComponent<PrefabDatabaseTestAuthoring>().Folder = _folder;

            BakingUtility.BakeGameObjects(World, new[] { _baked }, new BakingSettings(BakingUtility.BakingFlags.AssignName, _blobAssetStore));
        }

        private void UpdateDatabaseSystem()
        {
            World.GetOrCreateSystem<PrefabDatabaseSystem>().Update(WorldUnmanaged);
        }

        private DynamicHashMap<PrefabDefinitionHashMap, PrefabId, Entity> Definitions() =>
            Manager.CreateEntityQuery(typeof(PrefabDefinitionHashMap))
                   .GetSingletonBuffer<PrefabDefinitionHashMap>(true)
                   .AsHashMap<PrefabDefinitionHashMap, PrefabId, Entity>(true);

        private DynamicHashMap<PrefabMigrationHashMap, PrefabId, PrefabId> Migrations() =>
            Manager.CreateEntityQuery(typeof(PrefabMigrationHashMap))
                   .GetSingletonBuffer<PrefabMigrationHashMap>(true)
                   .AsHashMap<PrefabMigrationHashMap, PrefabId, PrefabId>(true);
    }
}