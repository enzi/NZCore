// <copyright project="NZCore.Tests" file="PrefabIdBakingTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NZCore.AssetManagement;
using NZCore.Tests.Authoring;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace NZCore.Tests.AssetManagement
{
    public class PrefabIdBakingTests : EcsTestsFixture
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
            _folder = TestAssets.CreateFolder("PrefabIdBakingTestsTemp");
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

        [TestCase(PrefabDefinition.DefaultKey)]
        [TestCase("Projectile")]
        public void FromDefinition_ReturnsTheIdTheAllocatorReservedForTheKey(string key)
        {
            var definition = CreateDefinition("Projectile");
            var reservedId = definition.ReservedIds.Single(reserved => reserved.Key == key).Id;

            Assert.AreEqual(new PrefabId(_group.AutoID, reservedId), Bake(definition, key).FromDefinition);
        }

        [Test]
        public void FromDefinition_MissingKeyIsABakingErrorAndReservesNothingBecauseBakersCantWriteAssets()
        {
            var definition = CreateDefinition();
            var reservedIds = definition.ReservedIds.ToArray();

            LogAssert.Expect(LogType.Error, new Regex("^Definition has no ID for key Unknown"));

            Assert.AreEqual(PrefabId.Null, Bake(definition, "Unknown").FromDefinition);
            CollectionAssert.AreEqual(reservedIds, definition.ReservedIds);
        }

        [Test]
        public void FromDefinition_DependsOnTheDefinitionAndItsGroupSoChangingEitherRebakes()
        {
            var definition = CreateDefinition();

            Bake(definition);

            CollectionAssert.IsSupersetOf(BakeDependencies(), new[] { GuidOf(definition), GuidOf(_group) });
        }

        [Test]
        public void FromAutoID_CombinesTheBoundGroupsIdWithTheAssetsAutoID()
        {
            Bind(_group);
            var asset = TestAssets.Create<CappedAutoIDTestAsset>(_folder, "Capped");

            Assert.AreEqual(new PrefabId(_group.AutoID, asset.AutoID), Bake(autoIDAsset: asset).FromAutoID);
        }

        [Test]
        public void FromAutoID_WithAKey_UsesTheGroupBoundToThatKeySoOneAssetCanBuildSeveralPrefabs()
        {
            Bind(_group);
            var projectiles = TestAssets.Create<PrefabGroup>(_folder, "Projectiles");
            Bind(projectiles, "Projectile");
            var asset = TestAssets.Create<CappedAutoIDTestAsset>(_folder, "Capped");

            var defaultId = Bake(autoIDAsset: asset).FromAutoID;
            var projectileId = Bake(autoIDAsset: asset, autoIDKey: "Projectile").FromAutoID;

            Assert.AreEqual(new PrefabId(_group.AutoID, asset.AutoID), defaultId);
            Assert.AreEqual(new PrefabId(projectiles.AutoID, asset.AutoID), projectileId, "The local ID stays the asset's AutoID, only the group differs");
        }

        [Test]
        public void FromAutoID_WithAnUnboundKey_ReportsAnErrorNamingTheKey()
        {
            Bind(_group);
            var asset = TestAssets.Create<CappedAutoIDTestAsset>(_folder, "Capped");

            LogAssert.Expect(LogType.Error, new Regex("^No PrefabGroup is bound to CappedAutoIDTestAsset with key Projectile, so Capped has no PrefabId"));

            Assert.AreEqual(PrefabId.Null, Bake(autoIDAsset: asset, autoIDKey: "Projectile").FromAutoID);
        }

        [Test]
        public void FromAutoID_ReportsAnErrorWhenNoGroupIsBound()
        {
            var asset = TestAssets.Create<CappedAutoIDTestAsset>(_folder, "Capped");

            LogAssert.Expect(LogType.Error, new Regex("^No PrefabGroup is bound to CappedAutoIDTestAsset, so Capped has no PrefabId"));

            Assert.AreEqual(PrefabId.Null, Bake(autoIDAsset: asset).FromAutoID);
        }

        [Test]
        public void FromAutoID_ReportsAnErrorWhenMoreThanOneGroupIsBound()
        {
            Bind(_group);
            var second = TestAssets.Create<PrefabGroup>(_folder, "Second");

            LogAssert.Expect(LogType.Error, new Regex(@"^CappedAutoIDTestAsset is bound to more than one PrefabGroup: [^.]*$"));
            Bind(second);

            var asset = TestAssets.Create<CappedAutoIDTestAsset>(_folder, "Capped");

            LogAssert.Expect(LogType.Error, new Regex(@"^CappedAutoIDTestAsset is bound to more than one PrefabGroup: .*\. Capped has no PrefabId"));

            Assert.AreEqual(PrefabId.Null, Bake(autoIDAsset: asset).FromAutoID);
        }

        [TestCase(0)]
        [TestCase(PrefabId.MaxId + 1)]
        public void FromAutoID_ReportsAnErrorWhenTheAutoIDDoesNotFitInAPrefabId(int autoID)
        {
            Bind(_group);
            var asset = ScriptableObject.CreateInstance<CappedAutoIDTestAsset>();
            asset.name = "OutOfRange";
            asset.AutoID = autoID;

            LogAssert.Expect(LogType.Error, new Regex($"^OutOfRange has AutoID {autoID}, but a PrefabId needs an ID between 1 and {PrefabId.MaxId}"));

            Assert.AreEqual(PrefabId.Null, Bake(autoIDAsset: asset).FromAutoID);
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void FromAutoID_SeesABindingChangeInTheNextBakeBecauseGroupsAreOnlyCachedForOneBake()
        {
            Bind(_group);
            var other = TestAssets.Create<PrefabGroup>(_folder, "Other");
            var asset = TestAssets.Create<CappedAutoIDTestAsset>(_folder, "Capped");

            Assert.AreEqual(new PrefabId(_group.AutoID, asset.AutoID), Bake(autoIDAsset: asset).FromAutoID);

            _group.BoundType.Type = null;
            TestAssets.Save(_group);
            Bind(other);

            Assert.AreEqual(new PrefabId(other.AutoID, asset.AutoID), Bake(autoIDAsset: asset).FromAutoID);
        }

        [Test]
        public void FromAutoID_DependsOnTheAssetAndEveryGroupSoBindingAnyGroupRebakes()
        {
            Bind(_group);
            var unbound = TestAssets.Create<PrefabGroup>(_folder, "Unbound");
            var asset = TestAssets.Create<CappedAutoIDTestAsset>(_folder, "Capped");

            Bake(autoIDAsset: asset);

            CollectionAssert.IsSupersetOf(BakeDependencies(), new[] { GuidOf(asset), GuidOf(_group), GuidOf(unbound) });
        }

        private PrefabDefinition CreateDefinition(params string[] prefabNames)
        {
            var source = TestAssets.Create<MultiPrefabSourceTestAsset>(_folder, "Source", asset => asset.Names = prefabNames.ToList());

            return TestAssets.Create<PrefabDefinition>(_folder, "Definition", definition =>
            {
                definition.Group = _group;
                definition.Target = source;
            });
        }

        private static void Bind(PrefabGroup group, string key = "")
        {
            group.BoundType.Type = typeof(CappedAutoIDTestAsset);
            group.BoundKey = key;
            TestAssets.Save(group);
        }

        private PrefabIdBakingTestResult Bake(PrefabDefinition definition = null, string key = null, ScriptableObjectWithAutoID autoIDAsset = null,
            string autoIDKey = null)
        {
            Object.DestroyImmediate(_baked);
            _baked = new GameObject("Baked");
            var authoring = _baked.AddComponent<PrefabIdBakingTestAuthoring>();
            authoring.Definition = definition;
            authoring.Key = key;
            authoring.AutoIDAsset = autoIDAsset;
            authoring.AutoIDKey = autoIDKey;

            BakingUtility.BakeGameObjects(World, new[] { _baked }, new BakingSettings(BakingUtility.BakingFlags.AssignName, _blobAssetStore));

            return Manager.CreateEntityQuery(typeof(PrefabIdBakingTestResult)).GetSingleton<PrefabIdBakingTestResult>();
        }

        private List<string> BakeDependencies()
        {
            var dependencies = World.GetExistingSystemManaged<BakingSystem>().GetAllAssetDependencies();
            var guids = new List<string>();

            for (var i = 0; i < dependencies.Length; i++)
            {
                guids.Add(dependencies[i].GUID.ToString());
            }

            return guids;
        }

        private static string GuidOf(Object asset) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
    }
}