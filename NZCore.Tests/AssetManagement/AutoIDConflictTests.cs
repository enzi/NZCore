// <copyright project="NZCore.Tests" file="AutoIDConflictTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using NUnit.Framework;
using NZCore.AssetManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace NZCore.Tests.AssetManagement
{
    public class AutoIDConflictTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = TestAssets.CreateFolder("AutoIDConflictTestsTemp");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(_folder);
        }

        [Test]
        public void DuplicateIdInErrorMode_BothKeepItAndReportAnErrorUntilOneGetsANewId()
        {
            var first = TestAssets.Create<PrefabGroup>(_folder, "First");
            var second = TestAssets.Create<PrefabGroup>(_folder, "Second");
            var sharedId = first.AutoID;

            LogAssert.Expect(LogType.Error, new Regex($"^Second and First share PrefabGroup AutoID {sharedId}\\. Each keeps it"));
            second.AutoID = sharedId;
            TestAssets.Save(second);

            Assert.AreEqual(sharedId, first.AutoID);
            Assert.AreEqual(sharedId, second.AutoID, "The ID may already be in save files, so it must not change without a report");
            Assert.IsTrue(AutoIDConflicts.TryGetOthers(first, out var others), "The inspector of each duplicate must show the Assign new ID box");
            CollectionAssert.AreEqual(new Object[] { second }, others);

            LogAssert.Expect(LogType.Error, new Regex($"^First and Second share PrefabGroup AutoID {sharedId}\\."));
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(first), ImportAssetOptions.ForceUpdate);

            AutoIDConflicts.AssignNewId(second);

            Assert.AreEqual(sharedId, first.AutoID);
            Assert.AreNotEqual(sharedId, second.AutoID);
            Assert.AreNotEqual(0, second.AutoID);
            Assert.IsFalse(AutoIDConflicts.TryGetOthers(first, out _));
        }

        [Test]
        public void DuplicateIdInRenumberMode_IsStillRenumberedWithoutAnError()
        {
            var first = TestAssets.Create<RenumberAutoIDTestAsset>(_folder, "First");
            var second = TestAssets.Create<RenumberAutoIDTestAsset>(_folder, "Second");
            var firstId = first.AutoID;

            second.AutoID = firstId;
            TestAssets.Save(second);

            Assert.AreEqual(firstId, first.AutoID);
            Assert.AreNotEqual(firstId, second.AutoID);
            Assert.AreNotEqual(0, second.AutoID);
        }

        [Test]
        public void GroupBoundToARenumberType_ReportsAnError()
        {
            var group = TestAssets.Create<PrefabGroup>(_folder, "Bound");

            LogAssert.Expect(LogType.Error,
                new Regex("^RenumberAutoIDTestAsset is bound to PrefabGroup Bound, but RenumberAutoIDTestAsset renumbers duplicate AutoIDs"));
            group.BoundType.Type = typeof(RenumberAutoIDTestAsset);
            TestAssets.Save(group);
        }
    }
}