// <copyright project="NZCore.Hybrid" file="HybridEntity_Authoring.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.Components;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Hash128 = Unity.Entities.Hash128;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NZCore.Hybrid
{
    public enum HybridEntityResourceType
    {
        GameObject,
        Addressable
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
        public HybridEntityResourceType resourceType;
        public GameObject prefab;
        public AssetReference addressable;
        public bool setTransform;
        public bool usePooling;
        public bool DestroyWithEntity;

#if UNITY_EDITOR

        private Mesh mesh;
        private Material mat;

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;

            if (prefab == null)
                return;

            var mr = prefab.GetComponentInChildren<MeshRenderer>();

            if (mr != null)
            {
                var mf = prefab.GetComponentInChildren<MeshFilter>();
                mesh = mf.sharedMesh;
                mat = mr.sharedMaterial;
            }
            else
            {
                var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>();

                if (smr == null)
                    return;

                mesh = smr.sharedMesh;
                mat = smr.sharedMaterial;
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
            if (mesh == null || mat == null)
                return;

            var tmpTransform = transform;
            var matrix = Matrix4x4.TRS(tmpTransform.position, tmpTransform.rotation, tmpTransform.localScale);
            Graphics.DrawMesh(mesh, matrix, mat, gameObject.layer, drawCamera);
        }

        
#endif

        public class HybridEntity_Authoring_Baker : Baker<HybridEntity_Authoring>
        {
            public override void Bake(HybridEntity_Authoring authoring)
            {
                if ((authoring.resourceType == HybridEntityResourceType.GameObject && authoring.prefab == null) ||
                    (authoring.resourceType == HybridEntityResourceType.Addressable && authoring.addressable == null))
                    return;

                var entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.WorldSpace);
                var presentationEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, authoring.name + "_PresentationSpawner");

                //this.AddRemoveFromLinkedEntityGroup(presentationEntity, entity);

                AddComponent(presentationEntity, new RemoveFromLinkedEntityGroupCleanupSetup()
                {
                    Parent = entity
                });
                
                AddComponent(entity, new HybridAnimator());
                AddComponent(entity, new AnimatorOverride());
                AddComponent(entity, new AnimatorOverrideState());

                if (authoring.resourceType == HybridEntityResourceType.GameObject && authoring.prefab != null)
                {
                    if (authoring.usePooling)
                    {
                        AddComponent(presentationEntity, new HybridSpawnPrefabFromPool()
                        {
                            HybridEntity = entity,
                            Prefab = authoring.prefab != null ? new WeakObjectReference<GameObject>(authoring.prefab) : default,
                            SetTransform = authoring.setTransform.ToByte(),
                            DestroyWithEntity = authoring.DestroyWithEntity.ToByte()
                        });
                    }
                    else
                    {
                        AddComponent(presentationEntity, new HybridSpawnPrefab()
                        {
                            HybridEntity = entity,
                            Prefab = authoring.prefab != null ? new WeakObjectReference<GameObject>(authoring.prefab) : default,
                            SetTransform = authoring.setTransform.ToByte(),
                            DestroyWithEntity = authoring.DestroyWithEntity.ToByte()
                        });
                    }
                }
                else if (authoring.resourceType == HybridEntityResourceType.Addressable && authoring.addressable != null)
                {
                    if (authoring.usePooling)
                    {
                        AddComponent(presentationEntity, new HybridSpawnAddressableFromPool()
                        {
                            HybridEntity = entity,
                            AddressableHash = new Hash128(authoring.addressable.AssetGUID),
                            SetTransform = authoring.setTransform.ToByte(),
                            DestroyWithEntity = authoring.DestroyWithEntity.ToByte()
                        });
                    }
                    else
                    {
                        AddComponent(presentationEntity, new HybridSpawnAddressable()
                        {
                            HybridEntity = entity,
                            AddressableHash = new Hash128(authoring.addressable.AssetGUID),
                            SetTransform = authoring.setTransform.ToByte(),
                            DestroyWithEntity = authoring.DestroyWithEntity.ToByte()
                        });
                    }
                }
            }
        }
    }
}