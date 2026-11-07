// <copyright project="NZCore.Tests" file="PrefabDefinitionToolingTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NZCore.AssetManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace NZCore.Tests.AssetManagement
{
    public class PrefabDefinitionToolingTests
    {
        private string _folder;
        private PrefabGroup _group;

        [SetUp]
        public void Setup()
        {
            _folder = TestAssets.CreateFolder("PrefabDefinitionToolingTestsTemp");
            _group = TestAssets.Create<PrefabGroup>(_folder, "Group");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(_folder);
        }

        [Test]
        public void CreateDefinitions_GivesEachPrefabAnAllocatedDefinitionAndAnAuthoringThatPointsBack()
        {
            var prefabs = new[] { CreatePrefab("A", _folder), CreatePrefab("B", _folder) };

            var created = PrefabDefinitionTooling.CreateDefinitions(prefabs, _group, _folder);

            Assert.AreEqual(2, created.Count);

            for (var i = 0; i < prefabs.Length; i++)
            {
                Assert.AreEqual(prefabs[i], created[i].Target);
                Assert.AreEqual(_group, created[i].Group);
                Assert.AreEqual(_folder, Path.GetDirectoryName(AssetDatabase.GetAssetPath(created[i]))!.Replace('\\', '/'));
                Assert.IsTrue(created[i].TryGetPrefabId(PrefabDefinition.DefaultKey, out _), "The prefab must be savable right after the one menu command");
                Assert.AreEqual(created[i], prefabs[i].GetComponent<PrefabDefinitionAuthoring>().Definition);
            }
        }

        [Test]
        public void CreateDefinitions_TwoPrefabsWithTheSameNameGetTwoDefinitionsInsteadOfOneOverwritingTheOther()
        {
            var otherFolder = $"{_folder}/Other";
            AssetDatabase.CreateFolder(_folder, "Other");
            var prefabs = new[] { CreatePrefab("Same", _folder), CreatePrefab("Same", otherFolder) };

            var created = PrefabDefinitionTooling.CreateDefinitions(prefabs, _group, _folder);

            Assert.AreEqual(2, created.Count);
            Assert.AreNotEqual(AssetDatabase.GetAssetPath(created[0]), AssetDatabase.GetAssetPath(created[1]));
            Assert.AreEqual(prefabs[0], created[0].Target);
            Assert.AreEqual(prefabs[1], created[1].Target);
        }

        [Test]
        public void CreateDefinitions_SkipsAPrefabThatAlreadyBelongsToADefinition()
        {
            var prefab = CreatePrefab("A", _folder);
            var owner = PrefabDefinitionTooling.CreateDefinitions(new[] { prefab }, _group, _folder)[0];

            LogAssert.Expect(LogType.Warning, new Regex($"^A already belongs to {owner.name}, so it gets no new definition"));

            Assert.IsEmpty(PrefabDefinitionTooling.CreateDefinitions(new[] { prefab }, _group, _folder));
            Assert.AreEqual(owner, prefab.GetComponent<PrefabDefinitionAuthoring>().Definition);
        }

        [Test]
        public void CreateDefinitions_AlsoGivesEveryVariantOfASelectedPrefabItsOwnDefinition()
        {
            var basePrefab = CreatePrefab("Base", _folder);
            var variant = TestAssets.CreateVariant(_folder, basePrefab, "Variant");

            var created = PrefabDefinitionTooling.CreateDefinitions(new[] { basePrefab }, _group, _folder);

            Assert.AreEqual(2, created.Count);
            Assert.AreEqual(created.Single(definition => definition.Target == basePrefab), basePrefab.GetComponent<PrefabDefinitionAuthoring>().Definition);
            Assert.AreEqual(created.Single(definition => definition.Target == variant), variant.GetComponent<PrefabDefinitionAuthoring>().Definition,
                "A variant must bake its own PrefabId, not its base's");
            Assert.AreEqual(1, variant.GetComponents<PrefabDefinitionAuthoring>().Length,
                "The variant overrides the inherited authoring instead of adding a second one");
        }

        [Test]
        public void CreateDefinitions_GivesAVariantItsOwnDefinitionEvenWhenItsBaseAlreadyHasOne()
        {
            var basePrefab = CreatePrefab("Base", _folder);
            var baseDefinition = PrefabDefinitionTooling.CreateDefinitions(new[] { basePrefab }, _group, _folder)[0];
            var variant = TestAssets.CreateVariant(_folder, basePrefab, "Variant");

            LogAssert.Expect(LogType.Warning, new Regex($"^Base already belongs to {baseDefinition.name}"));

            var created = PrefabDefinitionTooling.CreateDefinitions(new[] { basePrefab }, _group, _folder);

            Assert.AreEqual(variant, created.Single().Target);
            Assert.AreEqual(created[0], variant.GetComponent<PrefabDefinitionAuthoring>().Definition);
            Assert.AreEqual(baseDefinition, basePrefab.GetComponent<PrefabDefinitionAuthoring>().Definition);
        }

        [Test]
        public void AssigningAnotherPrefab_MovesTheAuthoringOverAndKeepsTheId()
        {
            var first = CreatePrefab("A", _folder);
            var second = CreatePrefab("B", _folder);
            var definition = PrefabDefinitionTooling.CreateDefinitions(new[] { first }, _group, _folder)[0];
            Assert.IsTrue(definition.TryGetPrefabId(PrefabDefinition.DefaultKey, out var prefabId));

            Assert.IsTrue(PrefabDefinitionTooling.TryAssignTarget(definition, second, out var error), error);

            Assert.IsFalse(first.TryGetComponent<PrefabDefinitionAuthoring>(out _), "The old prefab must stop baking the ID, or two prefabs share it");
            Assert.AreEqual(definition, second.GetComponent<PrefabDefinitionAuthoring>().Definition);
            Assert.IsTrue(definition.TryGetPrefabId(PrefabDefinition.DefaultKey, out var newPrefabId));
            Assert.AreEqual(prefabId, newPrefabId);
        }

        [Test]
        public void AssigningAPrefabThatBelongsToAnotherDefinition_IsRejected()
        {
            var prefab = CreatePrefab("A", _folder);
            var owner = PrefabDefinitionTooling.CreateDefinitions(new[] { prefab }, _group, _folder)[0];
            var other = TestAssets.Create<PrefabDefinition>(_folder, "Other", definition => definition.Group = _group);

            Assert.IsFalse(PrefabDefinitionTooling.TryAssignTarget(other, prefab, out var error));

            StringAssert.Contains(owner.name, error);
            Assert.IsNull(other.Target);
            Assert.AreEqual(owner, prefab.GetComponent<PrefabDefinitionAuthoring>().Definition);
        }

        [Test]
        public void Retire_MovesTheDefinitionFreesItsPrefabForTheReplacementAndMapsItsIdToTheReplacementsId()
        {
            var prefab = CreatePrefab("A", _folder);
            var definition = PrefabDefinitionTooling.CreateDefinitions(new[] { prefab }, _group, _folder)[0];
            var replacement = TestAssets.Create<PrefabDefinition>(_folder, "Replacement", created => created.Group = _group);
            var migrations = TestAssets.Create<PrefabMigrationAsset>(_folder, "Migrations");
            Assert.IsTrue(definition.TryGetPrefabId(PrefabDefinition.DefaultKey, out var oldId));
            Assert.IsTrue(replacement.TryGetPrefabId(PrefabDefinition.DefaultKey, out var newId));

            Assert.IsTrue(PrefabDefinitionTooling.TryRetire(definition, replacement, migrations, out var error), error);

            Assert.IsTrue(definition.Retired);
            Assert.IsNull(definition.Target);
            Assert.IsFalse(prefab.TryGetComponent<PrefabDefinitionAuthoring>(out _));
            Assert.AreEqual($"{_folder}/Retired/Retired_ADefinition.asset", AssetDatabase.GetAssetPath(definition));
            CollectionAssert.AreEqual(new[] { new PrefabMigrationEntry { From = oldId, To = newId } }, migrations.Migrations);
            Assert.IsTrue(PrefabDefinitionTooling.TryAssignTarget(replacement, prefab, out error), error);
        }

        private static GameObject CreatePrefab(string name, string folder) => TestAssets.CreatePrefab(folder, new GameObject(name));
    }
}