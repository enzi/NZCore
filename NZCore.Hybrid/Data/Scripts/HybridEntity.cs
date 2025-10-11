// <copyright project="NZCore.Hybrid" file="HybridEntity.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using Unity.Entities.Content;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Hash128 = Unity.Entities.Hash128;

namespace NZCore.Hybrid
{
    public class HybridEntity : MonoBehaviour
    {
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
                Graph = playableGraph,
                Mixer = mixer
            };
        }
        
// #if UNITY_EDITOR
//         public static List<DeferredGizmo> DeferredGizmos = new();
//         private void OnDrawGizmos()
//         {
//             Gizmos.DrawCube(transform.position, Vector3.one);
//
//             var tmpTransform = transform;
//             var tmpPosition = tmpTransform.position;
//             Gizmos.color = Color.red;
//             Gizmos.DrawLine(tmpPosition, tmpPosition + (tmpTransform.forward * 6.0f));
//             Gizmos.color = Color.white;
//
//             foreach (var deferredGizmo in DeferredGizmos)
//             {
//                 Gizmos.color = deferredGizmo.Color;
//
//                 switch (deferredGizmo.Type)
//                 {
//                     case GizmoType.Sphere:
//                         Gizmos.DrawWireSphere(deferredGizmo.Position, deferredGizmo.Size.x * deferredGizmo.Scale.x);
//                         break;
//                     case GizmoType.Capsule:
//                     {
//                         var point2 = math.mul(deferredGizmo.Rotation, new Vector3(0, 0, deferredGizmo.Size.z * deferredGizmo.Scale.z));
//                         
//                         GizmosUtility.DrawWireCapsule(deferredGizmo.Position, deferredGizmo.Position + point2, deferredGizmo.Size.x * deferredGizmo.Scale.x);
//                         break;
//                     }
//                     case GizmoType.Box:
//                         break;
//                     default:
//                         throw new ArgumentOutOfRangeException();
//                 }
//             }
//             
//             DeferredGizmos.Clear();
//         }
// #endif
    }

    public struct HybridSpawnPrefab : IComponentData
    {
        public Entity HybridEntity;
        public WeakObjectReference<GameObject> Prefab;
        public byte SetTransform;
        public byte DestroyWithEntity;
    }

    public struct HybridSpawnPrefabFromPool : IComponentData
    {
        public Entity HybridEntity;
        public WeakObjectReference<GameObject> Prefab;
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
}