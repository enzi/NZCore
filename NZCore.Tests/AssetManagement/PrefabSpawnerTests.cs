// <copyright project="NZCore.Tests" file="PrefabSpawnerTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NZCore.AssetManagement;
using NZCore.Tests.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NZCore.Tests.AssetManagement
{
    public class PrefabSpawnerTests : EcsTestsFixture
    {
        private string _folder;
        private BlobAssetStore _blobAssetStore;
        private GameObject _spawnerObject;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _folder = TestAssets.CreateFolder("PrefabSpawnerTestsTemp");
            _blobAssetStore = new BlobAssetStore(128);
        }

        [TearDown]
        public override void TearDown()
        {
            Object.DestroyImmediate(_spawnerObject);
            AssetDatabase.DeleteAsset(_folder);
            _blobAssetStore.Dispose();
            base.TearDown();
        }

        [Test]
        public void SpawnedInstance_TakesEveryComponentOfTheOverrideEntityExceptEntitiesDefaults()
        {
            var overrideValues = Enumerable.Range(100, 40).ToArray();

            var prefab = Manager.CreateEntity(typeof(Prefab), typeof(LocalTransform));
            Manager.AddComponentData(prefab, new SpawnerOverrideTestData { Value = 1 });
            Manager.AddBuffer<SpawnerOverrideTestElement>(prefab).Add(new SpawnerOverrideTestElement { Value = 1 });

            var overrides = Manager.CreateEntity(typeof(Disabled));
            Manager.AddComponentData(overrides, LocalTransform.FromPosition(9, 9, 9));
            Manager.AddComponentData(overrides, new SpawnerOverrideTestData { Value = 42 });
            Manager.AddComponent<SpawnerOverrideTestTag>(overrides);

            var elements = Manager.AddBuffer<SpawnerOverrideTestElement>(overrides);

            foreach (var value in overrideValues)
            {
                elements.Add(new SpawnerOverrideTestElement { Value = value });
            }

            CreateSpawner(prefab, LocalTransform.FromPosition(1, 2, 3), overrides);

            World.CreateSystem<PrefabInstanceSpawnerSystem>().Update(WorldUnmanaged);

            var instance = Manager.CreateEntityQuery(typeof(SpawnerOverrideTestData)).GetSingletonEntity();
            Assert.AreEqual(42, Manager.GetComponentData<SpawnerOverrideTestData>(instance).Value);
            Assert.IsTrue(Manager.HasComponent<SpawnerOverrideTestTag>(instance), "An override the prefab doesn't have is added");
            CollectionAssert.AreEqual(overrideValues,
                Manager.GetBuffer<SpawnerOverrideTestElement>(instance).AsNativeArray().ToArray().Select(element => element.Value));
            Assert.AreEqual(new float3(1, 2, 3), Manager.GetComponentData<LocalTransform>(instance).Position,
                "LocalTransform is a default every baked child carries, so it must not replace the placed transform");
            Assert.IsFalse(Manager.HasComponent<Disabled>(instance), "The override entity is disabled, its instances are not");

            Assert.AreEqual(1, Manager.GetComponentData<SpawnerOverrideTestData>(prefab).Value, "Overrides change the instance, not the prefab");
            Assert.AreEqual(1, Manager.GetBuffer<SpawnerOverrideTestElement>(prefab).Length);
        }

        [Test]
        public void DisabledOverride_DisablesThePrefabsEnabledComponentOnTheInstance()
        {
            var prefab = Manager.CreateEntity(typeof(Prefab), typeof(LocalTransform));
            Manager.AddComponentData(prefab, new SpawnerOverrideTestEnableable { Value = 1 });

            var overrides = Manager.CreateEntity(typeof(Disabled));
            Manager.AddComponentData(overrides, new SpawnerOverrideTestEnableable { Value = 5 });
            Manager.SetComponentEnabled<SpawnerOverrideTestEnableable>(overrides, false);

            CreateSpawner(prefab, LocalTransform.Identity, overrides);

            World.CreateSystem<PrefabInstanceSpawnerSystem>().Update(WorldUnmanaged);

            var instance = Manager.CreateEntityQuery(typeof(LocalTransform)).GetSingletonEntity();
            Assert.AreEqual(5, Manager.GetComponentData<SpawnerOverrideTestEnableable>(instance).Value);
            Assert.IsFalse(Manager.IsComponentEnabled<SpawnerOverrideTestEnableable>(instance));
            Assert.IsTrue(Manager.IsComponentEnabled<SpawnerOverrideTestEnableable>(prefab));
        }

        [Test]
        public void SharedComponentOnTheOverrideEntity_IsAnErrorAndOnlyItIsSkipped()
        {
            var prefab = Manager.CreateEntity(typeof(Prefab), typeof(LocalTransform));

            var overrides = Manager.CreateEntity(typeof(Disabled));
            Manager.AddComponentData(overrides, new SpawnerOverrideTestData { Value = 42 });
            Manager.AddSharedComponent(overrides, new SpawnerOverrideTestShared { Value = 7 });

            CreateSpawner(prefab, LocalTransform.Identity, overrides);

            LogAssert.Expect(LogType.Error,
                new Regex("skips the override .*SpawnerOverrideTestShared, because a spawner copies only unmanaged components and buffers"));

            World.CreateSystem<PrefabInstanceSpawnerSystem>().Update(WorldUnmanaged);

            var instance = Manager.CreateEntityQuery(typeof(SpawnerOverrideTestData)).GetSingletonEntity();
            Assert.AreEqual(42, Manager.GetComponentData<SpawnerOverrideTestData>(instance).Value);
            Assert.IsFalse(Manager.HasComponent<SpawnerOverrideTestShared>(instance));
        }

        [Test]
        public void Spawner_StaysAfterItSpawnsWithItsRequestDisabledSoItNeverSpawnsTwice()
        {
            var prefab = Manager.CreateEntity(typeof(Prefab), typeof(LocalTransform));
            var spawner = CreateSpawner(prefab, LocalTransform.Identity, Entity.Null);

            var system = World.CreateSystem<PrefabInstanceSpawnerSystem>();
            system.Update(WorldUnmanaged);

            Assert.IsTrue(Manager.Exists(spawner), "The spawner keeps its prefab and overrides, a respawn spawns from it again");
            Assert.IsFalse(Manager.IsComponentEnabled<PrefabDefinitionSpawnRequest>(spawner),
                "The disabled request is what a save keeps, so a load doesn't spawn a second instance");

            system.Update(WorldUnmanaged);

            Assert.AreEqual(1, Manager.CreateEntityQuery(typeof(LocalTransform)).CalculateEntityCount());
        }

        [Test]
        public void SpawnerGameObjectInASubScene_CopiesWhatItsOverrideAuthoringBakes()
        {
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World,
                DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default)
                                          .Where(type => type.Assembly.GetName().Name is "Unity.Entities" or "Unity.Entities.Hybrid" or "Unity.Scenes"));

            var definition = CreateDefinition();
            Assert.IsTrue(definition.TryGetPrefabId(PrefabDefinition.DefaultKey, out var prefabId));

            var scenePath = $"{_folder}/Spawner.unity";
            var scene = TestSubScenes.CreateAndOpen(scenePath);
            var spawnerObject = new GameObject("Spawner");
            SceneManager.MoveGameObjectToScene(spawnerObject, scene);
            spawnerObject.transform.position = new Vector3(4, 5, 6);
            spawnerObject.AddComponent<PrefabSpawnerAuthoring>().Definition = definition;

            var overridesObject = new GameObject("Overrides");
            overridesObject.transform.SetParent(spawnerObject.transform, false);
            overridesObject.transform.localScale = new Vector3(1, 2, 1);
            overridesObject.AddComponent<PrefabDefinitionOverrideObjectAuthoring>();
            var overrideAuthoring = overridesObject.AddComponent<SpawnerOverrideTestAuthoring>();
            overrideAuthoring.Value = 42;
            overrideAuthoring.Target = spawnerObject;

            spawnerObject.AddComponent<PrefabDefinitionOverrideAuthoring>().Overrides = overridesObject;
            TestSubScenes.SaveAndClose(scene);

            TestSubScenes.Load(World, scenePath);
            World.CreateSystem<PrefabInstanceSpawnerSystem>().Update(WorldUnmanaged);

            var spawner = Manager.CreateEntityQuery(typeof(PrefabInstanceSpawner)).GetSingletonEntity();
            var instance = Manager.CreateEntityQuery(typeof(SpawnerOverrideTestData)).GetSingletonEntity();

            Assert.AreEqual(prefabId, Manager.GetComponentData<PrefabId>(instance),
                "A save must still see the definition's PrefabId on an overridden instance");
            Assert.AreEqual(42, Manager.GetComponentData<SpawnerOverrideTestData>(instance).Value,
                "The override entity is disabled, so the query returns only the instance");
            Assert.IsTrue(Manager.HasComponent<SpawnerOverrideTestTag>(instance));
            CollectionAssert.AreEqual(new[] { 42, 43 },
                Manager.GetBuffer<SpawnerOverrideTestElement>(instance).AsNativeArray().ToArray().Select(element => element.Value));
            Assert.AreEqual(spawner, Manager.GetComponentData<SpawnerOverrideTestEntityRef>(instance).Target,
                "Overrides stay real components, so their Entity fields survive the subscene and point at runtime entities");
            Assert.Less(math.distance(new float3(4, 5, 6), Manager.GetComponentData<LocalTransform>(instance).Position), 0.0001f);
            Assert.IsFalse(Manager.HasComponent<PostTransformMatrix>(instance), "The override object's scale is a transform default, not an override");
        }

        [Test]
        public void OverridesObjectWithoutTheMarker_IsABakingErrorAndTheSpawnerCopiesNothing()
        {
            _spawnerObject = new GameObject("Spawner");
            _spawnerObject.AddComponent<PrefabSpawnerAuthoring>().Definition = CreateDefinition();

            var overridesObject = new GameObject("Overrides");
            overridesObject.transform.SetParent(_spawnerObject.transform, false);
            overridesObject.AddComponent<SpawnerOverrideTestAuthoring>().Value = 42;

            _spawnerObject.AddComponent<PrefabDefinitionOverrideAuthoring>().Overrides = overridesObject;

            LogAssert.Expect(LogType.Error,
                new Regex("^Spawner uses Overrides as its Overrides object, but it has no PrefabDefinitionOverrideObjectAuthoring"));

            BakingUtility.BakeGameObjects(World, new[] { _spawnerObject }, new BakingSettings(BakingUtility.BakingFlags.AssignName, _blobAssetStore));

            var spawner = Manager.CreateEntityQuery(typeof(PrefabInstanceSpawner)).GetSingletonEntity();
            Assert.AreEqual(Entity.Null, Manager.GetComponentData<PrefabInstanceSpawner>(spawner).Overrides,
                "Without the marker the object isn't disabled, so it must not be used as an override entity");
        }

        private Entity CreateSpawner(Entity prefab, LocalTransform transform, Entity overrides)
        {
            var spawner = Manager.CreateEntity();
            Manager.AddComponentData(spawner, new PrefabInstanceSpawner { Prefab = prefab, Transform = transform, Overrides = overrides });
            Manager.AddComponent<PrefabDefinitionSpawnRequest>(spawner);
            return spawner;
        }

        private PrefabDefinition CreateDefinition()
        {
            return PrefabDefinitionTooling.CreateDefinitions(new[] { TestAssets.CreatePrefab(_folder, new GameObject("Spawned")) }, null, _folder)[0];
        }
    }
}
