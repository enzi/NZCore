// <copyright project="NZCore.Tests" file="PrefabDefinitionValidationTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NZCore.AssetManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace NZCore.Tests.AssetManagement
{
    public class PrefabDefinitionValidationTests
    {
        private string _folder;
        private PrefabGroup _group;

        [SetUp]
        public void Setup()
        {
            _folder = TestAssets.CreateFolder("PrefabDefinitionValidationTestsTemp");
            _group = TestAssets.Create<PrefabGroup>(_folder, "Group");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(_folder);
        }

        [Test]
        public void DefinitionCreatedFromAPrefab_HasNoProblems()
        {
            PrefabDefinitionTooling.CreateDefinitions(new[] { CreatePrefab("A") }, _group, _folder);

            var ownProblems = PrefabDefinitionValidation.FindProblems()
                                                        .Where(problem => AssetDatabase.GetAssetPath(problem.Asset).StartsWith(_folder))
                                                        .Select(problem => problem.Message);

            CollectionAssert.IsEmpty(ownProblems, "The normal workflow must not show up as a problem");
        }

        [Test]
        public void AuthoringThatPointsAtADefinitionWithADifferentPrefab_IsReportedOnThePrefab()
        {
            var prefab = CreatePrefab("A");
            var definition = PrefabDefinitionTooling.CreateDefinitions(new[] { prefab }, _group, _folder)[0];
            definition.Target = CreatePrefab("B");
            TestAssets.Save(definition);

            AssertProblem(prefab, $"^A has a PrefabDefinitionAuthoring that points at {definition.name}, but its prefab is B");
        }

        [Test]
        public void DefinitionWithoutAPrefab_IsReported()
        {
            var definition = CreateDefinition("Empty");

            AssertProblem(definition, "^Empty has no prefab");
        }

        [Test]
        public void PrefabWithoutAnAuthoringThatPointsBack_IsReportedOnTheDefinition()
        {
            var prefab = CreatePrefab("A");
            var definition = CreateDefinition("Definition", prefab);

            AssertProblem(definition, "^Definition's prefab A has no PrefabDefinitionAuthoring that points back at it");
        }

        [Test]
        public void DefinitionInABoundGroup_IsReported()
        {
            var definition = CreateDefinition("Definition");

            LogAssert.Expect(LogType.Error, new Regex("^Definition is in PrefabGroup Group, which is bound to CappedAutoIDTestAsset"));
            _group.BoundType.Type = typeof(CappedAutoIDTestAsset);
            TestAssets.Save(_group);

            AssertProblem(definition, "^Definition is in PrefabGroup Group, which is bound to CappedAutoIDTestAsset");
        }

        [Test]
        public void IdReservedTwice_IsReported()
        {
            var first = CreateDefinition("First");
            var second = CreateDefinition("Second");
            second.ReservedIds[0] = first.ReservedIds[0];

            Assert.That(PrefabDefinitionValidation.FindProblems().Any(problem => (problem.Asset == first || problem.Asset == second) &&
                                                                                 problem.Message.Contains("is reserved by more than one PrefabDefinition")));
        }

        [Test]
        public void FormerIdThatAnotherDefinitionReserves_IsReportedOnTheFormerOwner()
        {
            var current = CreateDefinition("Current");
            var former = CreateDefinition("Former");
            Assert.IsTrue(current.TryGetPrefabId(PrefabDefinition.DefaultKey, out var sharedId));
            former.FormerIds.Add(new FormerPrefabId { Key = PrefabDefinition.DefaultKey, Id = sharedId });

            AssertProblem(former, $"^{Regex.Escape(sharedId.ToString())} is a former ID of Former, but Current \\(default key\\) reserves it now");
        }

        [Test]
        public void RetiredDefinitionWithoutAMigration_IsReported()
        {
            var definition = PrefabDefinitionTooling.CreateDefinitions(new[] { CreatePrefab("A") }, _group, _folder)[0];
            Assert.IsTrue(PrefabDefinitionTooling.TryRetire(definition, null, null, out var error), error);

            AssertProblem(definition, "^Retired_ADefinition is retired, but has no migration for ");
        }

        [Test]
        public void MigrationCycle_IsReportedOnTheMigrationAsset()
        {
            var migrations = CreateMigrations((Id(9001), Id(9002)), (Id(9002), Id(9001)));

            AssertProblem(migrations, "^PrefabMigration has a cycle: ");
        }

        [Test]
        public void OldIdMappedToTwoTargets_IsReportedOnTheMigrationAsset()
        {
            var migrations = CreateMigrations((Id(9001), Id(9002)), (Id(9001), Id(9003)));

            AssertProblem(migrations, $"^PrefabMigration maps {Regex.Escape(Id(9001).ToString())} to more than one ID: ");
        }

        private PrefabId Id(int id) => new(_group.AutoID, id);

        private GameObject CreatePrefab(string name) => TestAssets.CreatePrefab(_folder, new GameObject(name));

        private PrefabDefinition CreateDefinition(string name, Object target = null) =>
            TestAssets.Create<PrefabDefinition>(_folder, name, definition =>
            {
                definition.Group = _group;
                definition.Target = target;
            });

        private PrefabMigrationAsset CreateMigrations(params (PrefabId From, PrefabId To)[] migrations) =>
            TestAssets.Create<PrefabMigrationAsset>(_folder, "Migrations", asset =>
                asset.Migrations = migrations.Select(migration => new PrefabMigrationEntry { From = migration.From, To = migration.To }).ToList());

        private static void AssertProblem(Object asset, string pattern)
        {
            var problems = PrefabDefinitionValidation.FindProblems();
            var messages = problems.Where(problem => problem.Asset == asset).Select(problem => problem.Message).ToList();

            Assert.That(messages.Any(message => Regex.IsMatch(message, pattern)),
                $"No problem on {asset.name} matches {pattern}. Its problems: {string.Join(" | ", messages)}");
        }
    }
}