// <copyright project="NZCore" file="PrefabDatabaseMaps.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore.AssetManagement
{
    [InternalBufferCapacity(0)]
    public struct PrefabDefinitionEntry : IBufferElementData
    {
        public PrefabId Id;
        public Entity Prefab;
    }

    [InternalBufferCapacity(0)]
    public struct PrefabDefinitionHashMap : IDynamicHashMap<PrefabId, Entity>
    {
        public byte Value;
    }

    [InternalBufferCapacity(0)]
    public struct PrefabMigrationHashMap : IDynamicHashMap<PrefabId, PrefabId>
    {
        public byte Value;
    }

    [InternalBufferCapacity(0)]
    public struct PrefabReplacementHashMap : IDynamicHashMap<PrefabId, PrefabId>
    {
        public byte Value;
    }
}