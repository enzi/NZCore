// <copyright project="NZCore.Tests" file="PrefabDefinitionTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NZCore.AssetManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace NZCore.Tests.AssetManagement
{
    public class PrefabDefinitionTests
    {
        private const string DefaultKey = PrefabDefinition.DefaultKey;

        private string _folder;
        private PrefabGroup _group;

        [SetUp]
        public void SetUp()
        {
            _folder = TestAssets.CreateFolder("PrefabDefinitionTestsTemp");
            _group = TestAssets.Create<PrefabGroup>(_folder, "Group");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(_folder);
        }

        [Test]
        public void NewDefinition_GetsAnIdForItsDefaultKeyOnImport()
        {
            var definition = CreateDefinition("Definition");

            Assert.IsTrue(definition.TryGetPrefabId(DefaultKey, out var prefabId));
            Assert.AreEqual(_group.AutoID, prefabId.Group);
            Assert.AreEqual(1, prefabId.Id);
        }

        [Test]
        public void MultiPrefabSource_GetsOneIdPerNameAndKeepsTheDefaultKeyForTheRoot()
        {
            var source = CreateSource("Source", "Projectile", "Explosion");
            var definition = CreateDefinition("Definition", source);

            CollectionAssert.AreEquivalent(new[] { DefaultKey, "Projectile", "Explosion" }, definition.ReservedIds.Select(reservedId => reservedId.Key));
            CollectionAssert.AllItemsAreUnique(definition.ReservedIds.Select(reservedId => reservedId.Id));
            CollectionAssert.DoesNotContain(definition.ReservedIds.Select(reservedId => reservedId.Id), 0);
        }

        [Test]
        public void RemovedKey_KeepsItsIdSoANewKeyNeverReusesIt()
        {
            var source = CreateSource("Source", "Projectile", "Explosion");
            var definition = CreateDefinition("Definition", source);
            var explosionId = IdOf(definition, "Explosion");

            source.Names = new List<string> { "Projectile", "Trail" };
            TestAssets.Save(source);

            Assert.AreEqual(explosionId, IdOf(definition, "Explosion"));
            Assert.AreNotEqual(0, IdOf(definition, "Trail"));
            Assert.AreNotEqual(explosionId, IdOf(definition, "Trail"));
        }

        [Test]
        public void SwappingThePrefab_KeepsTheId()
        {
            var prefabA = TestAssets.CreatePrefab(_folder, new GameObject("PrefabA"));
            var prefabB = TestAssets.CreatePrefab(_folder, new GameObject("PrefabB"));
            var definition = CreateDefinition("Definition", prefabA);
            var id = IdOf(definition, DefaultKey);

            definition.Target = prefabB;
            TestAssets.Save(definition);

            Assert.AreEqual(id, IdOf(definition, DefaultKey));
            Assert.AreEqual(1, definition.ReservedIds.Count);
        }

        [Test]
        public void TwoDefinitionsInOneGroup_NeverShareAnId()
        {
            var first = CreateDefinition("First");
            var second = CreateDefinition("Second");

            Assert.AreNotEqual(IdOf(first, DefaultKey), IdOf(second, DefaultKey));
        }

        [Test]
        public void DefinitionsImportedTogether_NeverShareAnId()
        {
            PrefabDefinition first;
            PrefabDefinition second;

            AssetDatabase.StartAssetEditing();

            try
            {
                first = CreateDefinition("First");
                second = CreateDefinition("Second");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Assert.AreNotEqual(0, IdOf(first, DefaultKey));
            Assert.AreNotEqual(IdOf(first, DefaultKey), IdOf(second, DefaultKey));
        }

        [Test]
        public void DuplicateIds_KeepBothIdsAndReportAnErrorNamingBothDefinitions()
        {
            var first = CreateDefinition("First", reservedId: 5);

            LogAssert.Expect(LogType.Error,
                new Regex(
                    @"^ID 5 in prefab group \d+ is reserved more than once: (First \(default key\), Second \(default key\)|Second \(default key\), First \(default key\))\."));
            var second = CreateDefinition("Second", reservedId: 5);

            Assert.AreEqual(5, IdOf(first, DefaultKey));
            Assert.AreEqual(5, IdOf(second, DefaultKey));
        }

        [Test]
        public void IdSetTo0_GetsANewFreeIdSoADuplicateCanBeResolved()
        {
            var first = CreateDefinition("First", reservedId: 5);

            LogAssert.Expect(LogType.Error, new Regex(@"^ID 5 in prefab group \d+ is reserved more than once"));
            var second = CreateDefinition("Second", reservedId: 5);

            second.ReservedIds[0] = new ReservedPrefabId { Key = DefaultKey, Id = 0 };
            TestAssets.Save(second);

            Assert.AreEqual(5, IdOf(first, DefaultKey));
            Assert.AreNotEqual(0, IdOf(second, DefaultKey));
            Assert.AreNotEqual(5, IdOf(second, DefaultKey));
        }

        [Test]
        public void DefinitionInABoundGroup_ReportsAnErrorAndGetsNoId()
        {
            _group.BoundType.Type = typeof(CappedAutoIDTestAsset);
            TestAssets.Save(_group);

            LogAssert.Expect(LogType.Error, new Regex("^Definition is in PrefabGroup Group, which is bound to CappedAutoIDTestAsset"));
            var definition = CreateDefinition("Definition");

            Assert.IsEmpty(definition.ReservedIds);
        }

        [Test]
        public void ChangingTheGroup_GivesEveryReservedIdANewIdAndKeepsTheOldOnesAsFormerIdsSoOldSavesCanMigrate()
        {
            var definition = CreateDefinition("Definition", CreateSource("Source", "Projectile"));
            var oldIds = definition.ReservedIds.Select(reservedId => (reservedId.Key, new PrefabId(_group.AutoID, reservedId.Id))).ToArray();
            var newGroup = TestAssets.Create<PrefabGroup>(_folder, "NewGroup");

            definition.Group = newGroup;
            TestAssets.Save(definition);

            CollectionAssert.AreEquivalent(oldIds, definition.FormerIds.Select(formerId => (formerId.Key, formerId.Id)));
            Assert.AreEqual(newGroup.AutoID, definition.ReservedGroupId);

            foreach (var key in new[] { DefaultKey, "Projectile" })
            {
                Assert.IsTrue(definition.TryGetPrefabId(key, out var prefabId));
                Assert.AreEqual(newGroup.AutoID, prefabId.Group);
            }
        }

        [Test]
        public void FormerIds_AreNeverHandedOutAgainSoAnOldSaveCantResolveToANewPrefab()
        {
            var moved = CreateDefinition("Moved");
            var formerId = IdOf(moved, DefaultKey);

            moved.Group = TestAssets.Create<PrefabGroup>(_folder, "NewGroup");
            TestAssets.Save(moved);

            var next = CreateDefinition("Next");

            Assert.AreNotEqual(formerId, IdOf(next, DefaultKey));
        }

        [Test]
        public void RenumberingTheGroup_KeepsTheOldIdsAsFormerIds()
        {
            var definition = CreateDefinition("Definition");
            var oldId = new PrefabId(_group.AutoID, IdOf(definition, DefaultKey));
            var usedGroupIds = new HashSet<int>(PrefabGroup.FindAll().Select(group => group.AutoID));
            var newGroupId = Enumerable.Range(2, PrefabId.MaxGroup - 1).First(groupId => !usedGroupIds.Contains(groupId));

            _group.AutoID = newGroupId;
            TestAssets.Save(_group);

            Assert.AreEqual(newGroupId, definition.ReservedGroupId);
            CollectionAssert.AreEqual(new[] { oldId }, definition.FormerIds.Select(formerId => formerId.Id));
        }

        [Test]
        public void TryFindOwner_FindsTheDefinitionByItsCurrentAndItsFormerIdsAfterAGroupChange()
        {
            var definition = CreateDefinition("Definition");
            var oldId = new PrefabId(_group.AutoID, IdOf(definition, DefaultKey));

            Assert.IsTrue(PrefabDefinition.TryFindOwner(oldId, out var owner, out var isFormerId));
            Assert.AreEqual(definition, owner);
            Assert.IsFalse(isFormerId);

            definition.Group = TestAssets.Create<PrefabGroup>(_folder, "NewGroup");
            TestAssets.Save(definition);
            Assert.IsTrue(definition.TryGetPrefabId(DefaultKey, out var newId));

            Assert.IsTrue(PrefabDefinition.TryFindOwner(newId, out owner, out isFormerId));
            Assert.AreEqual(definition, owner);
            Assert.IsFalse(isFormerId);

            Assert.IsTrue(PrefabDefinition.TryFindOwner(oldId, out owner, out isFormerId));
            Assert.AreEqual(definition, owner);
            Assert.IsTrue(isFormerId);
        }

        private PrefabDefinition CreateDefinition(string name, Object target = null, int reservedId = 0)
        {
            return TestAssets.Create<PrefabDefinition>(_folder, name, definition =>
            {
                definition.Group = _group;
                definition.Target = target;

                if (reservedId != 0)
                {
                    definition.ReservedIds.Add(new ReservedPrefabId { Key = DefaultKey, Id = reservedId });
                    definition.ReservedGroupId = _group.AutoID;
                }
            });
        }

        private MultiPrefabSourceTestAsset CreateSource(string name, params string[] prefabNames)
        {
            return TestAssets.Create<MultiPrefabSourceTestAsset>(_folder, name, source => source.Names = prefabNames.ToList());
        }

        private static int IdOf(PrefabDefinition definition, string key)
        {
            return definition.ReservedIds.Single(reservedId => reservedId.Key == key).Id;
        }
    }
}