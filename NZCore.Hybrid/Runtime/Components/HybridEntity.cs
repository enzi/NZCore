// <copyright project="NZCore.Hybrid" file="HybridEntity.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using NZCore.Editor;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
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
            {
                ecb.AddComponent(entity, CreatePlayableGraph(animator));
                ecb.AddComponent(entity, new AnimatorOverride());
                ecb.AddComponent(entity, new AnimatorOverrideState());
            }

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
            {
                entityManager.AddComponentObject(entity, CreatePlayableGraph(animator));
                entityManager.AddComponentData(entity, new AnimatorOverride());
                entityManager.AddComponentData(entity, new AnimatorOverrideState());
            }

            if (go.TryGetComponent<HybridEntity>(out var hybridEntityComp))
            {
                hybridEntityComp.Entity = entity;
            }
        }

        public static HybridAnimator CreatePlayableGraph(Animator animator)
        {
            var playableGraph = PlayableGraph.Create("HybridGraph");
            var output = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
            var mixer = AnimationMixerPlayable.Create(playableGraph, 2);
            output.SetSourcePlayable(mixer);

            var dummyClip = new AnimationClip();
            var clipPlayable = AnimationClipPlayable.Create(playableGraph, dummyClip);
            var controller = AnimatorControllerPlayable.Create(playableGraph, animator.runtimeAnimatorController);
                
            playableGraph.Connect(controller, 0, mixer, 0);
            playableGraph.Connect(clipPlayable, 0, mixer, 1);

            return new HybridAnimator()
            {
                Animator = animator,
                Graph = playableGraph,
                Mixer = mixer
            };
        }
        
#if UNITY_EDITOR
        public static List<DeferredGizmo> DeferredGizmos = new();
        private void OnDrawGizmos()
        {
            Gizmos.DrawCube(transform.position, Vector3.one);

            var tmpTransform = transform;
            var tmpPosition = tmpTransform.position;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(tmpPosition, tmpPosition + (tmpTransform.forward * 6.0f));
            Gizmos.color = Color.white;

            foreach (var deferredGizmo in DeferredGizmos)
            {
                Gizmos.color = deferredGizmo.Color;

                switch (deferredGizmo.Type)
                {
                    case GizmoType.Sphere:
                        Gizmos.DrawWireSphere(deferredGizmo.Position, deferredGizmo.Radius * deferredGizmo.Scale.x);
                        break;
                    case GizmoType.Capsule:
                    {
                        var point2 = math.mul(deferredGizmo.Rotation, new Vector3(0, 0, deferredGizmo.Length * deferredGizmo.Scale.z));
                        
                        GizmosUtility.DrawWireCapsule(deferredGizmo.Position, deferredGizmo.Position + point2, deferredGizmo.Radius * deferredGizmo.Scale.x);
                        break;
                    }
                    case GizmoType.Box:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            DeferredGizmos.Clear();
        }
#endif
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