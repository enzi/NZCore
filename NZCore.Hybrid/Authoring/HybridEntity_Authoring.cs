// <copyright project="NZCore.Hybrid" file="HybridEntity_Authoring.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.Components;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;
using Hash128 = Unity.Entities.Hash128;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NZCore.Hybrid
{
    public enum HybridEntityResourceType
    {
        GameObject,
        Addressable,
        Manual
    }

    public enum GizmoType
    {
        Sphere,
        Capsule,
        Box,
        Circle
    }

    public class DeferredGizmo
    {
        public Color Color;
        public GizmoType Type;
        public float3 Position;
        public Quaternion Rotation;
        public float3 Scale;
        public float3 Size;

        // public float Radius;
        // public float Height;
        // public float Length;
    }

    [ExecuteInEditMode]
    public class HybridEntity_Authoring : MonoBehaviour
    {
        [FormerlySerializedAs("resourceType")] public HybridEntityResourceType ResourceType;
        [FormerlySerializedAs("prefab")] public GameObject Prefab;
        [FormerlySerializedAs("addressable")] public AssetReference Addressable;
        [FormerlySerializedAs("setTransform")] public bool SetTransform;
        [FormerlySerializedAs("usePooling")] public bool UsePooling;
        public bool DestroyWithEntity;

#if UNITY_EDITOR

        private Mesh _mesh;
        private Material _mat;

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;

            if (Prefab == null)
            {
                return;
            }

            var mr = Prefab.GetComponentInChildren<MeshRenderer>();

            if (mr != null)
            {
                var mf = Prefab.GetComponentInChildren<MeshFilter>();
                _mesh = mf.sharedMesh;
                _mat = mr.sharedMaterial;
            }
            else
            {
                var smr = Prefab.GetComponentInChildren<SkinnedMeshRenderer>();

                if (smr == null)
                {
                    return;
                }

                _mesh = smr.sharedMesh;
                _mat = smr.sharedMaterial;
            }
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            Draw(sceneView.camera);
        }

        private void Draw(Camera drawCamera)
        {
            if (_mesh == null || _mat == null)
            {
                return;
            }

            var tmpTransform = transform;
            var matrix = Matrix4x4.TRS(tmpTransform.position, tmpTransform.rotation, tmpTransform.localScale);
            Graphics.DrawMesh(_mesh, matrix, _mat, gameObject.layer, drawCamera);
        }


#endif

        public class HybridEntity_Authoring_Baker : Baker<HybridEntity_Authoring>
        {
            public override void Bake(HybridEntity_Authoring authoring)
            {
                if ((authoring.ResourceType == HybridEntityResourceType.GameObject && authoring.Prefab == null) ||
                    (authoring.ResourceType == HybridEntityResourceType.Addressable && authoring.Addressable == null))
                {
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.WorldSpace);
                
                AddComponent(entity, new HybridAnimator());
                AddComponent(entity, new AnimatorOverride());
               
                switch (authoring.ResourceType)
                {
                    case HybridEntityResourceType.GameObject when authoring.Prefab != null:
                    {
                        var presentationEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, authoring.name + "_PresentationSpawner");
                        AddComponent(presentationEntity, new RemoveFromLinkedEntityGroupCleanupSetup { Parent = entity });
                        
                        if (authoring.UsePooling)
                        {
                            Debug.LogError("TODO pooling is not working");
                            
                            AddComponent(presentationEntity, new HybridSpawnPrefabFromPool
                            {
                                HybridEntity = entity,
                                Prefab = authoring.Prefab != null ? new WeakObjectReference<GameObject>(authoring.Prefab) : default,
                                SetTransform = authoring.SetTransform.ToByte(),
                                DestroyWithEntity = authoring.DestroyWithEntity.ToByte()
                            });
                        }
                        else
                        {
                            AddComponent(presentationEntity, new HybridSpawnPrefab
                            {
                                HybridEntity = entity,
                                Prefab = authoring.Prefab != null ? new WeakObjectReference<GameObject>(authoring.Prefab) : default,
                                SetTransform = authoring.SetTransform.ToByte(),
                                DestroyWithEntity = authoring.DestroyWithEntity.ToByte()
                            });
                        }

                        break;
                    }
                    case HybridEntityResourceType.Addressable when authoring.Addressable != null:
                    {
                        var presentationEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, authoring.name + "_PresentationSpawner");
                        AddComponent(presentationEntity, new RemoveFromLinkedEntityGroupCleanupSetup { Parent = entity });
                        
                        if (authoring.UsePooling)
                        {
                            Debug.LogError("TODO pooling is not working");
                            AddComponent(presentationEntity, new HybridSpawnAddressableFromPool
                            {
                                HybridEntity = entity,
                                AddressableHash = new Hash128(authoring.Addressable.AssetGUID),
                                SetTransform = authoring.SetTransform.ToByte(),
                                DestroyWithEntity = authoring.DestroyWithEntity.ToByte()
                            });
                        }
                        else
                        {
                            AddComponent(presentationEntity, new HybridSpawnAddressable
                            {
                                HybridEntity = entity,
                                AddressableHash = new Hash128(authoring.Addressable.AssetGUID),
                                SetTransform = authoring.SetTransform.ToByte(),
                                DestroyWithEntity = authoring.DestroyWithEntity.ToByte()
                            });
                        }

                        break;
                    }
                    case HybridEntityResourceType.Manual:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}