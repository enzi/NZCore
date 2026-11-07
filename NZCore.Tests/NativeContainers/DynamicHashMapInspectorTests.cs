// <copyright project="NZCore.Tests" file="DynamicHashMapInspectorTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using NUnit.Framework;
using NZCore.AssetManagement;
using NZCore.Editor;
using Unity.Entities;

namespace NZCore.Tests
{
    public class DynamicHashMapInspectorTests : EcsTestsFixture
    {
        private struct UntypedHashMap : IDynamicHashMap
        {
            public byte Value;
        }

        [Test]
        public void TypedMap_ListsEveryKeyAndValueInsteadOfRawBytes()
        {
            var entity = Manager.CreateEntity();
            var map = Manager.AddBuffer<PrefabMigrationHashMap>(entity).AsHashMap<PrefabMigrationHashMap, PrefabId, PrefabId>();
            map.TryAdd(new PrefabId(1, 1), new PrefabId(2, 1));
            map.TryAdd(new PrefabId(1, 2), new PrefabId(2, 2));

            var entries = new List<(string Key, string Value)>();
            DynamicHashMapReader.Create(typeof(PrefabMigrationHashMap)).Read(Manager, entity, entries);

            CollectionAssert.AreEquivalent(new[]
            {
                (new PrefabId(1, 1).ToString(), new PrefabId(2, 1).ToString()),
                (new PrefabId(1, 2).ToString(), new PrefabId(2, 2).ToString())
            }, entries);
        }

        [Test]
        public void BufferWithoutTheTypedMarker_HasNoReaderSoTheInspectorShowsTheDefaultList()
        {
            Assert.IsNull(DynamicHashMapReader.Create(typeof(UntypedHashMap)));
            Assert.IsNull(DynamicHashMapReader.Create(typeof(LinkedEntityGroup)));
        }
    }
}