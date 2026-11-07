// <copyright project="NZCore.Tests.Authoring" file="SpawnerOverrideTestAuthoring.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using UnityEngine;

namespace NZCore.Tests.Authoring
{
    public class SpawnerOverrideTestAuthoring : MonoBehaviour
    {
        public int Value;

        private class Baker : Baker<SpawnerOverrideTestAuthoring>
        {
            public override void Bake(SpawnerOverrideTestAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new SpawnerOverrideTestData { Value = authoring.Value });
                AddComponent<SpawnerOverrideTestTag>(entity);

                var elements = AddBuffer<SpawnerOverrideTestElement>(entity);
                elements.Add(new SpawnerOverrideTestElement { Value = authoring.Value });
                elements.Add(new SpawnerOverrideTestElement { Value = authoring.Value + 1 });
            }
        }
    }

    public struct SpawnerOverrideTestData : IComponentData
    {
        public int Value;
    }

    public struct SpawnerOverrideTestTag : IComponentData { }

    public struct SpawnerOverrideTestElement : IBufferElementData
    {
        public int Value;
    }
}