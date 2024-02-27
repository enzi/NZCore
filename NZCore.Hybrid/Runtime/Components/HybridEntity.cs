using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace NZCore.Hybrid
{
    public class HybridEntity : MonoBehaviour
    {
        public Entity Entity = Entity.Null;
        
        public static void LinkEntityToGameObject(EntityCommandBuffer ecb, Entity entity, GameObject go, bool setTransform = true)
        {
            var transform = go.transform;
            
            World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<TransformEntityMapping>().AddTransform(transform, entity);

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
        
        public static void LinkEntityToGameObject(EntityManager entityManager, Entity entity, GameObject go, bool setTransform = true)
        {
            var transform = go.transform;
            
            World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<TransformEntityMapping>().AddTransform(transform, entity);

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
        public Entity hybridEntity;
        public GameObject prefab;
        public bool setTransform;
    }

    public class HybridSpawnPrefabFromPool : IComponentData
    {
        public Entity hybridEntity;
        public GameObject prefab;
        public bool setTransform;
    }
    
    public struct HybridSpawnAddressable : IComponentData
    {
        public Entity hybridEntity;
        public Hash128 addressableHash;
        public bool setTransform;
    }

    public struct HybridSpawnAddressableFromPool : IComponentData
    {
        public Entity hybridEntity;
        public Hash128 addressableHash;
        public bool setTransform;
    }

    public class WaitForHybridEntity : CustomYieldInstruction
    {
        private readonly HybridEntity hybridEntity;

        public WaitForHybridEntity(HybridEntity hybridEntity)
        {
            this.hybridEntity = hybridEntity;
        }

        public override bool keepWaiting => hybridEntity == null || hybridEntity.Entity == Entity.Null;
    }
}