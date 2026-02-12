using AOT;
using NZCore;
using NZCore.AssetManagement;
using NZCore.Hybrid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Content;
using UnityEngine;

namespace NZSpellCasting
{
#if NZSPELLCASTING
    [UpdateInGroup(typeof(NZSpellCastingInitializationSystemGroup))]
#else
    [UpdateInGroup(typeof(NZCoreInitializationSystemGroup))]
#endif
    [UpdateAfter(typeof(EntityRemapSystem))]
    public partial struct HybridInstantiationSystem : ISystem, ISystemStartStop
    {
        private TransformEntityMappingSingleton entityMapping;
        private WeakAssetLoaderSingleton assetLoader;
        
        private delegate void InstantiateGameObjects(in TransformEntityMappingSingleton mapping, in NativeArray<InstantiateGameObjectsRequest> requests);
        private ManagedDelegate<InstantiateGameObjects> instantiateGameObjectsFunction;
        private NativeList<InstantiateGameObjectsRequest> instantiateRequests;
        
        public void OnCreate(ref SystemState state)
        {
            instantiateRequests = new NativeList<InstantiateGameObjectsRequest>(0, Allocator.Persistent);
            instantiateGameObjectsFunction = new ManagedDelegate<InstantiateGameObjects>(Instantiate);
           
            state.RequireForUpdate<TransformEntityMappingSingleton>();
            state.RequireForUpdate<WeakAssetLoaderSingleton>();
            state.RequireForUpdate<EntityRemapBuffer>();
        }
        
        public void OnStartRunning(ref SystemState state)
        {
            entityMapping = SystemAPI.GetSingleton<TransformEntityMappingSingleton>();
            assetLoader = SystemAPI.GetSingleton<WeakAssetLoaderSingleton>();
        }

        public void OnStopRunning(ref SystemState state)
        {
        }
        
        public void OnDestroy(ref SystemState state)
        {
            instantiateRequests.Dispose();
            instantiateGameObjectsFunction.Dispose();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //return;
            
            state.EntityManager.CompleteDependencyBeforeRO<CreateHybridObjectRequestSingleton>();

            var remapBuffer = SystemAPI.GetSingletonBuffer<EntityRemapBuffer>(true).AsNativeArray();
            var createHybrids = SystemAPI.GetSingleton<CreateHybridObjectRequestSingleton>();
            var createEnumerator = createHybrids.Requests.GetEnumerator();
            
            instantiateRequests.Clear();
            
            while (createEnumerator.MoveNext())
            {
                ref var list = ref createEnumerator.Current;
        
                for (int i = list.Length - 1; i >= 0; i--)
                {
                    var request = list[i];

                    if (!assetLoader.Load(request.PrefabToLoad))
                    {
                        list.RemoveAt(i);
                    }
                        
                    if (!assetLoader.HasLoaded(request.PrefabToLoad))
                    {
                        continue;
                    }
                        
                    instantiateRequests.Add(new InstantiateGameObjectsRequest()
                    {
                        Request = request
                    });
                    
                    assetLoader.RegisterGeneric(request.PrefabToLoad);
                    
                    list.RemoveAt(i);
                }
            }

            if (instantiateRequests.Length > 0)
            {
                instantiateGameObjectsFunction.Ptr.Invoke(entityMapping, instantiateRequests.AsArray());

                foreach (var finishedRequest in instantiateRequests)
                {
                    if (finishedRequest.Request.DestroyTime > 0)
                    {
                        entityMapping.TrackedGameObjects.Add(new TrackedGameObject()
                        {
                            Prefab = finishedRequest.Request.PrefabToLoad,
                            Object = finishedRequest.Result.Instance,
                            DestroyTime = finishedRequest.Request.DestroyTime
                        });
                    }

                    Entity bindToEntity;
                    if (finishedRequest.Request.BindToEntity.Index < 0)
                    {
                        remapBuffer.GetRemappedEntity(finishedRequest.Request.BindToEntity, out bindToEntity);
                    }
                    else
                    {
                        bindToEntity = finishedRequest.Request.BindToEntity;
                    }

                    if (bindToEntity != Entity.Null)
                    {
                        entityMapping.AddTransform(
                            finishedRequest.Result.TransformInstanceId, 
                            bindToEntity,
                            finishedRequest.Result.Instance, 
                            finishedRequest.Result.Animator,
                            bindToEntity != Entity.Null, 
                            finishedRequest.Request.DestroyTime);
                        
                        state.EntityManager.SetComponentData(bindToEntity, finishedRequest.Result.HybridAnimator);

                        if (SystemAPI.HasBuffer<HybridObjectBuffer>(bindToEntity))
                        {
                            var hybridBuffer = SystemAPI.GetBuffer<HybridObjectBuffer>(bindToEntity);

                            // destroy all previous elements
                            foreach (var element in hybridBuffer)
                            {
                                entityMapping.DestroyRequests.Add(element.GameObject);
                            }

                            hybridBuffer.Add(new HybridObjectBuffer()
                            {
                                GameObject = finishedRequest.Result.Instance
                            });
                        }
                    }
                }
            }
        }
        
        [MonoPInvokeCallback(typeof(InstantiateGameObjects))]
        public static void Instantiate(in TransformEntityMappingSingleton mapping, in NativeArray<InstantiateGameObjectsRequest> requests)
        {
            for (int i = 0; i < requests.Length; i++)
            {
                ref var request = ref requests.ElementAt(i);
        
                if (request.Request.PrefabToLoad.LoadingStatus != ObjectLoadingStatus.Completed)
                {
#if UNITY_EDITOR
                    Debug.LogError($"Prefab {request.Request.PrefabToLoad} has not been loaded yet!");
#endif
                    continue;
                }

                GameObject instance = null;

                switch (request.Request.SpawnMode)
                {
                    case CreateHybridSpawnMode.Position:
                    {
                        instance = Object.Instantiate(request.Request.PrefabToLoad.Result, 
                            request.Request.PositionData.Position + request.Request.PositionData.Offset, Quaternion.identity);
                        
                        break;
                    }
                    case CreateHybridSpawnMode.Locator:
                    {
                       if (!mapping.TryGetTransform(request.Request.LocatorData.Entity, out Transform targetTransform))
                       {
                           //Debug.Log($"No transform found for {entity}");
                           continue;
                       }
                        
                       if (request.Request.LocatorData.Locator == LocatorPosition.None)
                       {
                           instance  = Object.Instantiate(request.Request.PrefabToLoad.Result, targetTransform.position, targetTransform.rotation);
                       }
                       else
                       {
                           var locators = targetTransform.GetComponentInChildren<Locators>();
                           Transform parent;
                           Vector3 offset;
                        
                           switch (request.Request.LocatorData.Locator)
                           {
                               case LocatorPosition.Head:
                               {
                                   parent = locators.Head;
                                   offset = locators.HeadOffset;
                                   break;
                               }
                               case LocatorPosition.HandLeft:
                               {
                                   parent = locators.HandLeft;
                                   offset = locators.HandLeftOffset;
                                   break;
                               }
                               case LocatorPosition.HandRight:
                               {
                                   parent = locators.HandRight;
                                   offset = locators.HandRightOffset;
                                   break;
                               }
                               case LocatorPosition.Spine:
                               {
                                   parent = locators.Spine;
                                   offset = locators.SpineOffset;
                                   break;
                               }
                               case LocatorPosition.FeetLeft:
                               {
                                   parent = locators.FeetLeft;
                                   offset = locators.FeetLeftOffset;
                                   break;
                               }
                               case LocatorPosition.FeetRight:
                               {
                                   parent = locators.FeetRight;
                                   offset = locators.FeetRightOffset;
                                   break;
                               }
                               // case HybridCasterSystem.LocatorPosition.FeetBetween:
                               // {
                               //     parent = locators.FeetRight;
                               //     offset = locators.HeadOffset;
                               //     break;
                               // }
                               case LocatorPosition.WeaponLeft:
                               {
                                   parent = locators.WeaponLeft;
                                   offset = locators.WeaponLeftOffset;
                                   break;
                               }
                               case LocatorPosition.WeaponRight:
                               {
                                   parent = locators.WeaponRight;
                                   offset = locators.WeaponRightOffset;
                                   break;
                               }
                               default:
                                   throw new System.ArgumentOutOfRangeException(nameof(request.Request.LocatorData.Locator), request.Request.LocatorData.Locator, null);
                           }
                        
                           if (request.Request.LocatorData.AttachToParent)
                           {
                               instance = Object.Instantiate(request.Request.PrefabToLoad.Result, parent);
                               instance.transform.localPosition = offset;
                           }
                           else
                           {
                               instance = Object.Instantiate(request.Request.PrefabToLoad.Result);
                               instance.transform.position = parent.transform.position + offset;
                           }
                       }
                        
                       break;
                    }
                }

                if (instance != null)
                {
                    request.Result.Instance = instance;
                    request.Result.TransformInstanceId = instance.transform.GetInstanceID();
                    
                    var animator = instance.GetComponent<Animator>();
                    if (animator != null)
                    {
                        request.Result.Animator = animator;
                        request.Result.HybridAnimator = HybridEntity.CreatePlayableGraph(animator);
                    }
                }
            }
        }
        
        public struct InstantiateGameObjectsRequest
        {
            public CreateHybridObjectRequest Request;
            public InstantiateResult Result;
        }

        public struct InstantiateResult
        {
            public UnityObjectRef<GameObject> Instance;
            public UnityObjectRef<Animator> Animator;
            public HybridAnimator HybridAnimator;
            public int TransformInstanceId;
        }
    }
}