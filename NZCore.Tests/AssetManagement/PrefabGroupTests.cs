// <copyright project="NZCore.Tests" file="PrefabGroupTests.cs">
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
    public class PrefabGroupTests
    {
        private const string GenericPath = "Packages/com.enzisoft.nzcore/NZCore.Authoring/AssetManagement/Prefabs/Generic.asset";

        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = TestAssets.CreateFolder("PrefabGroupTestsTemp");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(_folder);
        }

        [Test]
        public void Generic_HoldsId1SoDefinitionsWithoutAGroupAlwaysGetTheSameGroup()
        {
            var generic = AssetDatabase.LoadAssetAtPath<PrefabGroup>(GenericPath);

            Assert.IsNotNull(generic);
            Assert.AreEqual(PrefabGroup.GenericId, generic.AutoID);
        }

        [Test]
        public void NewGroup_GetsTheLowestFreeIdStartingAt2()
        {
            var usedIds = new HashSet<int>(FindGroups().Select(group => group.AutoID));
            var expectedId = Enumerable.Range(1, PrefabId.MaxGroup).First(id => !usedIds.Contains(id));

            var group = TestAssets.Create<PrefabGroup>(_folder, "NewGroup");

            Assert.AreEqual(expectedId, group.AutoID);
            Assert.GreaterOrEqual(group.AutoID, 2);
        }

        [Test]
        public void BindingOneTypeToTwoGroups_ReportsAnErrorNamingBothGroups()
        {
            TestAssets.Create<PrefabGroup>(_folder, "BoundA", group => group.BoundType.Type = typeof(CappedAutoIDTestAsset));
            var groupB = TestAssets.Create<PrefabGroup>(_folder, "BoundB");

            LogAssert.Expect(LogType.Error, new Regex("^CappedAutoIDTestAsset is bound to more than one PrefabGroup: (BoundA, BoundB|BoundB, BoundA)$"));
            groupB.BoundType.Type = typeof(CappedAutoIDTestAsset);
            TestAssets.Save(groupB);
        }

        [Test]
        public void GroupIdAbove255_ReportsAnError()
        {
            LogAssert.Expect(LogType.Error, new Regex("^TooHigh has AutoID 256, but the highest PrefabGroup ID is 255"));
            TestAssets.Create<PrefabGroup>(_folder, "TooHigh", group => group.AutoID = PrefabId.MaxGroup + 1);
        }

        [Test]
        public void Allocator_HandsOutIdsUpToTheTypesMaximumThenReportsAnErrorInsteadOfGoingPastIt()
        {
            for (var id = 1; id <= CappedAutoIDTestAsset.MaxId; id++)
            {
                Assert.AreEqual(id, TestAssets.Create<CappedAutoIDTestAsset>(_folder, $"Capped{id}").AutoID);
            }

            LogAssert.Expect(LogType.Error, new Regex("run out of IDs for CappedAutoIDTestAsset! The highest ID is 3"));
            var overflow = TestAssets.Create<CappedAutoIDTestAsset>(_folder, "Overflow");

            Assert.AreEqual(0, overflow.AutoID);
        }

        private static IEnumerable<PrefabGroup> FindGroups() =>
            AssetDatabase.FindAssets($"t:{nameof(PrefabGroup)}")
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Distinct()
                         .Select(AssetDatabase.LoadAssetAtPath<PrefabGroup>);
    }
}