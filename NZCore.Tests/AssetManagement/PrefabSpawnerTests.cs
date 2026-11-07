// <copyright project="NZCore.Tests" file="PrefabSpawnerTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Linq;
using NUnit.Framework;
using NZCore.AssetManagement;
using NZCore.Tests.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NZCore.Tests.AssetManagement
{
    public class PrefabSpawnerTests : EcsTestsFixture
    {
        private string _folder;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _folder = TestAssets.CreateFolder("PrefabSpawnerTestsTemp");
        }

        [TearDown]
        public override void TearDown()
        {
            AssetDatabase.DeleteAsset(_folder);
            base.TearDown();
        }

        [Test]
        public void SpawnedInstance_TakesTheSpawnersOwnComponentsButKeepsThePlacedTransform()
        {
            var overrideValues = Enumerable.Range(100, 40).ToArray();

            var prefab = Manager.CreateEntity(typeof(Prefab), typeof(LocalTransform));
            Manager.AddComponentData(prefab, new SpawnerOverrideTestData { Value = 1 });
            Manager.AddBuffer<SpawnerOverrideTestElement>(prefab).Add(new SpawnerOverrideTestElement { Value = 1 });

            var spawner = Manager.CreateEntity();
            Manager.AddComponentData(spawner, new PrefabInstanceSpawner { Prefab = prefab, Transform = LocalTransform.FromPosition(1, 2, 3) });
            Manager.AddComponentData(spawner, LocalTransform.FromPosition(9, 9, 9));
            Manager.AddComponentData(spawner, new SpawnerOverrideTestData { Value = 42 });
            Manager.AddComponent<SpawnerOverrideTestTag>(spawner);

            var elements = Manager.AddBuffer<SpawnerOverrideTestElement>(spawner);

            foreach (var value in overrideValues)
            {
                elements.Add(new SpawnerOverrideTestElement { Value = value });
            }

            World.CreateSystem<PrefabInstanceSpawnerSystem>().Update(WorldUnmanaged);

            Assert.IsFalse(Manager.Exists(spawner));

            var instance = Manager.CreateEntityQuery(typeof(SpawnerOverrideTestData)).GetSingletonEntity();
            Assert.AreEqual(42, Manager.GetComponentData<SpawnerOverrideTestData>(instance).Value);
            Assert.IsTrue(Manager.HasComponent<SpawnerOverrideTestTag>(instance), "A component the prefab doesn't have is added");
            CollectionAssert.AreEqual(overrideValues,
                Manager.GetBuffer<SpawnerOverrideTestElement>(instance).AsNativeArray().ToArray().Select(element => element.Value));
            Assert.AreEqual(new float3(1, 2, 3), Manager.GetComponentData<LocalTransform>(instance).Position,
                "The spawner's own LocalTransform is a Unity component, so it must not replace the placed transform");

            Assert.AreEqual(1, Manager.GetComponentData<SpawnerOverrideTestData>(prefab).Value, "Overrides change the instance, not the prefab");
            Assert.AreEqual(1, Manager.GetBuffer<SpawnerOverrideTestElement>(prefab).Length);
        }

        [Test]
        public void SpawnerGameObjectInASubScene_SpawnsTheDefinitionsPrefabWithTheOverridesOfItsOtherComponents()
        {
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World,
                DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default)
                                          .Where(type => type.Assembly.GetName().Name is "Unity.Entities" or "Unity.Entities.Hybrid" or "Unity.Scenes"));

            var definition = PrefabDefinitionTooling.CreateDefinitions(new[] { TestAssets.CreatePrefab(_folder, new GameObject("Spawned")) }, null, _folder)[0];
            Assert.IsTrue(definition.TryGetPrefabId(PrefabDefinition.DefaultKey, out var prefabId));

            var scenePath = $"{_folder}/Spawner.unity";
            var scene = TestSubScenes.CreateAndOpen(scenePath);
            var spawnerObject = new GameObject("Spawner");
            SceneManager.MoveGameObjectToScene(spawnerObject, scene);
            spawnerObject.transform.position = new Vector3(4, 5, 6);
            spawnerObject.AddComponent<PrefabSpawnerAuthoring>().Definition = definition;
            spawnerObject.AddComponent<SpawnerOverrideTestAuthoring>().Value = 42;
            TestSubScenes.SaveAndClose(scene);

            TestSubScenes.Load(World, scenePath);
            World.CreateSystem<PrefabInstanceSpawnerSystem>().Update(WorldUnmanaged);

            var instance = Manager.CreateEntityQuery(typeof(PrefabId)).ToEntityArray(Allocator.Temp).ToArray().Single();
            Assert.AreEqual(prefabId, Manager.GetComponentData<PrefabId>(instance),
                "A save must still see the definition's PrefabId on an overridden instance");
            Assert.AreEqual(42, Manager.GetComponentData<SpawnerOverrideTestData>(instance).Value);
            Assert.Less(math.distance(new float3(4, 5, 6), Manager.GetComponentData<LocalTransform>(instance).Position), 0.0001f);
        }
    }
}