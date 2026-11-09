// <copyright project="NZCore.Tests" file="PrefabDefinitionAuthoringTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using NUnit.Framework;
using NZCore.AssetManagement;
using NZCore.Tests.Authoring;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace NZCore.Tests.AssetManagement
{
    public class PrefabDefinitionAuthoringTests : EcsTestsFixture
    {
        private string _folder;
        private BlobAssetStore _blobAssetStore;
        private GameObject _reference;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _blobAssetStore = new BlobAssetStore(128);
            _folder = TestAssets.CreateFolder("PrefabDefinitionAuthoringTestsTemp");
        }

        [TearDown]
        public override void TearDown()
        {
            Object.DestroyImmediate(_reference);
            AssetDatabase.DeleteAsset(_folder);
            _blobAssetStore.Dispose();
            base.TearDown();
        }

        [Test]
        public void DefinitionOnANonRootGameObject_IsABakingErrorAndBakesNoPrefabIdSoSavesOnlySeeTheRootsId()
        {
            var group = TestAssets.Create<PrefabGroup>(_folder, "Group");
            var rootDefinition = TestAssets.Create<PrefabDefinition>(_folder, "RootDefinition", definition => definition.Group = group);
            var childDefinition = TestAssets.Create<PrefabDefinition>(_folder, "ChildDefinition", definition => definition.Group = group);

            var root = new GameObject("Root");
            root.AddComponent<PrefabDefinitionAuthoring>().Definition = rootDefinition;
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform);
            child.AddComponent<PrefabDefinitionAuthoring>().Definition = childDefinition;
            var prefab = TestAssets.CreatePrefab(_folder, root);

            rootDefinition.Target = prefab;
            TestAssets.Save(rootDefinition);
            Assert.IsTrue(rootDefinition.TryGetPrefabId(PrefabDefinition.DefaultKey, out var rootId));

            _reference = new GameObject("Reference");
            _reference.AddComponent<PrefabReferenceTestAuthoring>().Prefab = prefab;

            LogAssert.Expect(LogType.Error, new Regex("^Child has a PrefabDefinitionAuthoring but isn't the root of its prefab"));

            BakingUtility.BakeGameObjects(World, new[] { _reference }, new BakingSettings(BakingUtility.BakingFlags.AssignName, _blobAssetStore));

            var prefabIds = Manager.CreateEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<PrefabId>().WithOptions(EntityQueryOptions.IncludePrefab))
                                   .ToComponentDataArray<PrefabId>(Allocator.Temp);
            CollectionAssert.AreEqual(new[] { rootId }, prefabIds.ToArray());
        }
    }
}