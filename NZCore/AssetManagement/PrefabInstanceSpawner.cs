// <copyright project="NZCore" file="PrefabInstanceSpawner.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using Unity.Transforms;

namespace NZCore.AssetManagement
{
    public struct PrefabInstanceSpawner : IComponentData
    {
        public Entity Prefab;
        public LocalTransform Transform;
        public Entity Overrides;
    }

    public struct PrefabDefinitionSpawnRequest : IComponentData, IEnableableComponent, ISavable { }
}