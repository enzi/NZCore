// <copyright project="NZCore.Tests" file="PrefabDefinitionBakingTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.IO;
using System.Linq;
using NUnit.Framework;
using NZCore.AssetManagement;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace NZCore.Tests.AssetManagement
{
    public class PrefabDefinitionBakingTests : EcsTestsFixture
    {
        private const float PlacedScale = 2;

        private static readonly Vector3 PlacedPosition = new(1, 2, 3);
        private static readonly Quaternion PlacedRotation = Quaternion.Euler(0, 90, 0);

        private string _folder;
        private string _scenePath;
        private PrefabId _rootId;
        private PrefabId _childId;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World,
                DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default)
                                          .Where(type => type.Assembly.GetName().Name is "Unity.Entities" or "Unity.Entities.Hybrid" or "Unity.Scenes"));

            _folder = TestAssets.CreateFolder("PrefabDefinitionBakingTestsTemp");
            _scenePath = $"{_folder}/Placed.unity";

            var group = TestAssets.Create<PrefabGroup>(_folder, "Group");
            var rootDefinition = TestAssets.Create<PrefabDefinition>(_folder, "RootDefinition", definition => definition.Group = group);
            var childDefinition = TestAssets.Create<PrefabDefinition>(_folder, "ChildDefinition", definition => definition.Group = group);

            var root = new GameObject("Root");
            root.AddComponent<PrefabDefinitionAuthoring>().Definition = rootDefinition;
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform);
            child.AddComponent<PrefabDefinitionAuthoring>().Definition = childDefinition;
            new GameObject("Plain").transform.SetParent(child.transform);
            var prefab = TestAssets.CreatePrefab(_folder, root);

            rootDefinition.Target = prefab;
            TestAssets.Save(rootDefinition);

            Assert.IsTrue(rootDefinition.TryGetPrefabId(PrefabDefinition.DefaultKey, out _rootId));
            Assert.IsTrue(childDefinition.TryGetPrefabId(PrefabDefinition.DefaultKey, out _childId));

            File.WriteAllText(_scenePath, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
            AssetDatabase.ImportAsset(_scenePath);

            var scene = EditorSceneManager.OpenScene(_scenePath, OpenSceneMode.Additive);
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            placed.transform.SetPositionAndRotation(PlacedPosition, PlacedRotation);
            placed.transform.localScale = Vector3.one * PlacedScale;
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        [TearDown]
        public override void TearDown()
        {
            AssetDatabase.DeleteAsset(_folder);
            base.TearDown();
        }

        [Test]
        public void PlacedInstance_BakesToOneSpawnerWhosePrefabCarriesTheDefinitionsId()
        {
            LoadSubScene(_scenePath);

            var spawners = Query<PrefabInstanceSpawner>().ToComponentDataArray<PrefabInstanceSpawner>(Allocator.Temp);

            Assert.AreEqual(1, spawners.Length, "The nested child must not get a spawner of its own, the root's spawner spawns it");
            Assert.IsTrue(Manager.HasComponent<Prefab>(spawners[0].Prefab));
            Assert.AreEqual(_rootId, Manager.GetComponentData<PrefabId>(spawners[0].Prefab));
            var instanceEntities = Query<SceneTag>().ToEntityArray(Allocator.Temp).ToArray()
                                                    .Where(entity => !Manager.HasComponent<PrefabInstanceSpawner>(entity) && !IsSceneBookkeeping(entity))
                                                    .ToArray();
            Assert.IsEmpty(instanceEntities, "The placed instance and its children are stripped instead of loading as live entities");
        }

        [Test]
        public void Spawner_InstantiatesThePrefabAtThePlacedTransformAndRemovesItself()
        {
            LoadSubScene(_scenePath);

            World.CreateSystem<PrefabInstanceSpawnerSystem>().Update(WorldUnmanaged);

            Assert.IsTrue(Query<PrefabInstanceSpawner>().IsEmpty);

            var prefabIds = Query<PrefabId>().ToComponentDataArray<PrefabId>(Allocator.Temp);
            CollectionAssert.AreEquivalent(new[] { _rootId, _childId }, prefabIds.ToArray());

            var instance = Query<PrefabId>().ToEntityArray(Allocator.Temp).ToArray().Single(entity => Manager.GetComponentData<PrefabId>(entity) == _rootId);
            var transform = Manager.GetComponentData<LocalTransform>(instance);

            Assert.Less(math.distance((float3)PlacedPosition, transform.Position), 0.0001f);
            Assert.Greater(math.abs(math.dot(((quaternion)PlacedRotation).value, transform.Rotation.value)), 0.9999f);
            Assert.AreEqual(PlacedScale, transform.Scale, 0.0001f);
        }

        private EntityQuery Query<T>() => Manager.CreateEntityQuery(ComponentType.ReadOnly<T>());

        private bool IsSceneBookkeeping(Entity entity)
        {
            return Manager.GetComponentTypes(entity).ToArray().Any(type => type.GetManagedType().Name == "PublicEntityRef");
        }

        private void LoadSubScene(string scenePath)
        {
            var guid = AssetDatabase.AssetPathToGUID(scenePath);
            Assert.That(guid, Is.Not.Empty, $"Scene not found: {scenePath}");

            var sceneEntity = SceneSystem.LoadSceneAsync(
                World.Unmanaged,
                new Hash128(guid),
                new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                });

            var sceneSystemGroup = World.GetExistingSystemManaged<SceneSystemGroup>();

            for (var update = 0; update < 1000; update++)
            {
                sceneSystemGroup.Update();
                Manager.CompleteAllTrackedJobs();

                var state = SceneSystem.GetSceneStreamingState(World.Unmanaged, sceneEntity);

                if (state == SceneSystem.SceneStreamingState.LoadedSuccessfully)
                {
                    return;
                }

                Assert.That(state,
                    Is.Not.EqualTo(SceneSystem.SceneStreamingState.FailedLoadingSceneHeader)
                      .And.Not.EqualTo(SceneSystem.SceneStreamingState.LoadedWithSectionErrors),
                    $"Failed to load scene {scenePath}: {state}");
            }

            Assert.Fail($"Timed out loading scene: {scenePath}");
        }
    }
}