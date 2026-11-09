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
        public GameObject Target;

        private class Baker : Baker<SpawnerOverrideTestAuthoring>
        {
            public override void Bake(SpawnerOverrideTestAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new SpawnerOverrideTestData { Value = authoring.Value });
                AddComponent<SpawnerOverrideTestTag>(entity);
                AddComponent(entity, new SpawnerOverrideTestEntityRef { Target = GetEntity(authoring.Target, TransformUsageFlags.None) });

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

    public struct SpawnerOverrideTestEnableable : IComponentData, IEnableableComponent
    {
        public int Value;
    }

    public struct SpawnerOverrideTestEntityRef : IComponentData
    {
        public Entity Target;
    }

    public struct SpawnerOverrideTestShared : ISharedComponentData
    {
        public int Value;
    }
}
