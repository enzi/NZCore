// <copyright project="NZCore.Hybrid" file="HybridEntity.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace NZCore.Hybrid
{
    public class HybridEntity : MonoBehaviour
    {
        public Entity Entity = Entity.Null;

        public static void LinkEntityToGameObject(EntityCommandBuffer ecb, Entity entity, GameObject go, bool setTransform, bool destroyWithEntity)
        {
            var transform = go.transform;

            World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<TransformEntityMapping>().AddTransform(transform, entity, destroyWithEntity);

            if (setTransform)
            {
                ecb.SetComponent(entity, LocalTransform.FromPositionRotation(transform.position, transform.rotation));
            }

            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null)
                ecb.AddComponent(entity, animator);

            if (go.TryGetComponent<HybridEntity>(out var hybridEntityComp))
            {
                hybridEntityComp.Entity = entity;
            }
        }

        public static void LinkEntityToGameObject(EntityManager entityManager, Entity entity, GameObject go, bool setTransform, bool destroyWithEntity)
        {
            var transform = go.transform;

            World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<TransformEntityMapping>().AddTransform(transform, entity, destroyWithEntity);

            if (setTransform)
            {
                entityManager.SetComponentData(entity, LocalTransform.FromPositionRotation(transform.position, transform.rotation));
            }

            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null)
                entityManager.AddComponentObject(entity, animator);

            if (go.TryGetComponent<HybridEntity>(out var hybridEntityComp))
            {
                hybridEntityComp.Entity = entity;
            }
        }
    }

    public class HybridSpawnPrefab : IComponentData
    {
        public Entity HybridEntity;
        public GameObject Prefab;
        public byte SetTransform;
        public byte DestroyWithEntity;
    }

    public class HybridSpawnPrefabFromPool : IComponentData
    {
        public Entity HybridEntity;
        public GameObject Prefab;
        public byte SetTransform;
        public byte DestroyWithEntity;
    }

    public struct HybridSpawnAddressable : IComponentData
    {
        public Entity HybridEntity;
        public Hash128 AddressableHash;
        public byte SetTransform;
        public byte DestroyWithEntity;
    }

    public struct HybridSpawnAddressableFromPool : IComponentData
    {
        public Entity HybridEntity;
        public Hash128 AddressableHash;
        public byte SetTransform;
        public byte DestroyWithEntity;
    }

    public class WaitForHybridEntity : CustomYieldInstruction
    {
        private readonly HybridEntity HybridEntity;

        public WaitForHybridEntity(HybridEntity hybridEntity)
        {
            this.HybridEntity = hybridEntity;
        }

        public override bool keepWaiting => HybridEntity == null || HybridEntity.Entity == Entity.Null;
    }
}