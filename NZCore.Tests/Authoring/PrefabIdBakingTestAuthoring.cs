// <copyright project="NZCore.Tests.Authoring" file="PrefabIdBakingTestAuthoring.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.AssetManagement;
using Unity.Entities;
using UnityEngine;

namespace NZCore.Tests.Authoring
{
    public class PrefabIdBakingTestAuthoring : MonoBehaviour
    {
        public PrefabDefinition Definition;
        public string Key;
        public ScriptableObjectWithAutoID AutoIDAsset;
        public string AutoIDKey;

        private class Baker : Baker<PrefabIdBakingTestAuthoring>
        {
            public override void Bake(PrefabIdBakingTestAuthoring authoring)
            {
                AddComponent(GetEntity(TransformUsageFlags.None), new PrefabIdBakingTestResult
                {
                    FromDefinition = authoring.Definition != null ? PrefabIdBaking.FromDefinition(this, authoring.Definition, authoring.Key) : PrefabId.Null,
                    FromAutoID = authoring.AutoIDAsset != null ? PrefabIdBaking.FromAutoID(this, authoring.AutoIDAsset, authoring.AutoIDKey) : PrefabId.Null
                });
            }
        }
    }

    public struct PrefabIdBakingTestResult : IComponentData
    {
        public PrefabId FromDefinition;
        public PrefabId FromAutoID;
    }
}