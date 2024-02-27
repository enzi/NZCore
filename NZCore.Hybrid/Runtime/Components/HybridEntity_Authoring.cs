using NZCore.Components;
using Unity.Entities;
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
    
    
    [ExecuteInEditMode]
    public class HybridEntity_Authoring : MonoBehaviour
    {
        public HybridEntityResourceType resourceType;
        public GameObject prefab;
        public AssetReference addressable;
        public bool setTransform;
        public bool usePooling;

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
        
        private void OnDrawGizmos()
        {
            Gizmos.DrawCube(transform.position, Vector3.one);

            var tmpTransform = transform;
            var tmpPosition = tmpTransform.position;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(tmpPosition, tmpPosition + (tmpTransform.forward * 6.0f));
            Gizmos.color = Color.white;
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

                if (authoring.resourceType == HybridEntityResourceType.GameObject && authoring.prefab != null)
                {
                    if (authoring.usePooling)
                    {
                        AddComponentObject(presentationEntity, new HybridSpawnPrefabFromPool()
                        {
                            hybridEntity = entity,
                            prefab = authoring.prefab,
                            setTransform = authoring.setTransform
                        });
                    }
                    else
                    {
                        AddComponentObject(presentationEntity, new HybridSpawnPrefab()
                        {
                            hybridEntity = entity,
                            prefab = authoring.prefab,
                            setTransform = authoring.setTransform
                        });
                    }
                }
                else if (authoring.resourceType == HybridEntityResourceType.Addressable && authoring.addressable != null)
                {
                    if (authoring.usePooling)
                    {
                        AddComponent(presentationEntity, new HybridSpawnAddressableFromPool()
                        {
                            hybridEntity = entity,
                            addressableHash = new Hash128(authoring.addressable.AssetGUID),
                            setTransform = authoring.setTransform
                        });
                    }
                    else
                    {
                        AddComponent(presentationEntity, new HybridSpawnAddressable()
                        {
                            hybridEntity = entity,
                            addressableHash = new Hash128(authoring.addressable.AssetGUID),
                            setTransform = authoring.setTransform
                        });
                    }
                }

                // todo InvalidOperationException: Baking error: Attempt to set component Unity.Transforms.Translation for Baker Baker with authoring component HybridEntity_Authoring but the component was added by a different Baker LegacyBoxBaker
                //SetComponent(entity, new Translation() { Value = authoring.transform.position });
                //SetComponent(entity,new Rotation() { Value = authoring.transform.rotation });
            }
        }
    }
}