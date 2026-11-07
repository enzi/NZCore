// <copyright project="NZCore.Tests" file="PrefabIdTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NUnit.Framework;
using NZCore.AssetManagement;
using Unity.Collections;

namespace NZCore.Tests.AssetManagement
{
    public class PrefabIdTests
    {
        [Test]
        public void Value_KeepsGroupInTop8BitsAndIdInLow24BitsBecauseSaveFilesStoreTheRawInt()
        {
            var id = new PrefabId(3, 42);

            Assert.AreEqual((3 << 24) | 42, id.Value);
            Assert.AreEqual(3, id.Group);
            Assert.AreEqual(42, id.Id);
        }

        [TestCase(1, 1)]
        [TestCase(127, 1)]
        [TestCase(128, 1)]
        [TestCase(255, PrefabId.MaxId)]
        public void GroupAndId_RoundTripAcrossTheWholeRangeIncludingGroupsThatSetTheSignBit(int group, int id)
        {
            var prefabId = new PrefabId(group, id);

            Assert.AreEqual(group, prefabId.Group);
            Assert.AreEqual(id, prefabId.Id);
        }

        [Test]
        public void Equality_SameIdInDifferentGroupsIsADifferentPrefab()
        {
            Assert.AreNotEqual(new PrefabId(2, 7), new PrefabId(3, 7));
            Assert.IsTrue(new PrefabId(2, 7) != new PrefabId(3, 7));
            Assert.IsTrue(new PrefabId(2, 7) == new PrefabId(2, 7));
            Assert.AreEqual(new PrefabId(2, 7).GetHashCode(), new PrefabId(2, 7).GetHashCode());
        }

        [Test]
        public void Null_IsZeroAndEqualToDefaultSoUnsetComponentsReadAsNull()
        {
            Assert.AreEqual(0, PrefabId.Null.Value);
            Assert.AreEqual(PrefabId.Null, default(PrefabId));
            Assert.IsTrue(PrefabId.Null.IsNull);
        }

        [Test]
        public void IsNull_IsTrueForIdZeroInAnyGroup()
        {
            var id = new PrefabId { Value = 3 << 24 };

            Assert.IsTrue(id.IsNull);
        }

        [Test]
        public void IsNull_IsFalseForEveryConstructedId()
        {
            Assert.IsFalse(new PrefabId(1, 1).IsNull);
            Assert.IsFalse(new PrefabId(255, PrefabId.MaxId).IsNull);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(PrefabId.MaxGroup + 1)]
        public void Constructor_RejectsGroupOutside1To255(int group)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new PrefabId(group, 1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(PrefabId.MaxId + 1)]
        public void Constructor_RejectsIdThatIsNullOrWouldSpillIntoTheGroupBits(int id)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new PrefabId(3, id));
        }

        [Test]
        public void ToFixedString_FitsTheLongestId()
        {
            Assert.AreEqual((FixedString32Bytes)"Group:255 ID:16777215", new PrefabId(255, PrefabId.MaxId).ToFixedString());
            Assert.AreEqual((FixedString32Bytes)"Group:3 ID:42", new PrefabId(3, 42).ToFixedString());
        }
    }
}