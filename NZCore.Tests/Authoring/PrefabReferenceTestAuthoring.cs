// <copyright project="NZCore.Tests.Authoring" file="PrefabReferenceTestAuthoring.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using UnityEngine;

namespace NZCore.Tests.Authoring
{
    public class PrefabReferenceTestAuthoring : MonoBehaviour
    {
        public GameObject Prefab;

        private class Baker : Baker<PrefabReferenceTestAuthoring>
        {
            public override void Bake(PrefabReferenceTestAuthoring authoring)
            {
                AddComponent(GetEntity(TransformUsageFlags.None),
                    new PrefabReferenceTestData { Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic) });
            }
        }
    }

    public struct PrefabReferenceTestData : IComponentData
    {
        public Entity Prefab;
    }
}