// <copyright project="NZCore.Tests" file="PrefabDefinitionBakingTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Linq;
using NUnit.Framework;
using NZCore.AssetManagement;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace NZCore.Tests.AssetManagement
{
    public class PrefabDefinitionBakingTests : EcsTestsFixture
    {
        private const float PlacedScale = 2;

        private static readonly Vector3 PlacedPosition = new(1, 2, 3);
        private static readonly Quaternion PlacedRotation = Quaternion.Euler(0, 90, 0);

        private string _folder;
        private string _scenePath;
        private PrefabDefinition _rootDefinition;
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
            _rootDefinition = TestAssets.Create<PrefabDefinition>(_folder, "RootDefinition", definition => definition.Group = group);
            var childDefinition = TestAssets.Create<PrefabDefinition>(_folder, "ChildDefinition", definition => definition.Group = group);

            var root = new GameObject("Root");
            root.AddComponent<PrefabDefinitionAuthoring>().Definition = _rootDefinition;
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform);
            child.AddComponent<PrefabDefinitionAuthoring>().Definition = childDefinition;
            new GameObject("Plain").transform.SetParent(child.transform);
            var prefab = TestAssets.CreatePrefab(_folder, root);

            _rootDefinition.Target = prefab;
            TestAssets.Save(_rootDefinition);

            Assert.IsTrue(_rootDefinition.TryGetPrefabId(PrefabDefinition.DefaultKey, out _rootId));
            Assert.IsTrue(childDefinition.TryGetPrefabId(PrefabDefinition.DefaultKey, out _childId));

            var scene = TestSubScenes.CreateAndOpen(_scenePath);
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            placed.transform.SetPositionAndRotation(PlacedPosition, PlacedRotation);
            placed.transform.localScale = Vector3.one * PlacedScale;
            TestSubScenes.SaveAndClose(scene);
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
            TestSubScenes.Load(World, _scenePath);

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
        public void Spawner_InstantiatesThePrefabAtThePlacedTransformAndDisablesItsRequest()
        {
            TestSubScenes.Load(World, _scenePath);

            World.CreateSystem<PrefabInstanceSpawnerSystem>().Update(WorldUnmanaged);

            Assert.IsTrue(Query<PrefabDefinitionSpawnRequest>().IsEmpty);
            Assert.AreEqual(1, Manager.CreateEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                                                         .WithAll<PrefabInstanceSpawner>()
                                                         .WithDisabled<PrefabDefinitionSpawnRequest>())
                                      .CalculateEntityCount());

            var prefabIds = Query<PrefabId>().ToComponentDataArray<PrefabId>(Allocator.Temp);
            CollectionAssert.AreEquivalent(new[] { _rootId, _childId }, prefabIds.ToArray());

            var instance = Query<PrefabId>().ToEntityArray(Allocator.Temp).ToArray().Single(entity => Manager.GetComponentData<PrefabId>(entity) == _rootId);
            var transform = Manager.GetComponentData<LocalTransform>(instance);

            Assert.Less(math.distance((float3)PlacedPosition, transform.Position), 0.0001f);
            Assert.Greater(math.abs(math.dot(((quaternion)PlacedRotation).value, transform.Rotation.value)), 0.9999f);
            Assert.AreEqual(PlacedScale, transform.Scale, 0.0001f);
        }

        [Test]
        public void ChangingTheDefinitionsId_RebakesThePrefabWithTheNewId()
        {
            const int newId = 100;

            TestSubScenes.Unload(World, TestSubScenes.Load(World, _scenePath));

            _rootDefinition.ReservedIds[0] = new ReservedPrefabId { Key = PrefabDefinition.DefaultKey, Id = newId };
            TestAssets.Save(_rootDefinition);

            TestSubScenes.Load(World, _scenePath);

            var spawner = Query<PrefabInstanceSpawner>().GetSingleton<PrefabInstanceSpawner>();
            Assert.AreEqual(new PrefabId(_rootId.Group, newId), Manager.GetComponentData<PrefabId>(spawner.Prefab));
        }

        private EntityQuery Query<T>() => Manager.CreateEntityQuery(ComponentType.ReadOnly<T>());

        private bool IsSceneBookkeeping(Entity entity)
        {
            return Manager.GetComponentTypes(entity).ToArray().Any(type => type.GetManagedType().Name == "PublicEntityRef");
        }
    }
}