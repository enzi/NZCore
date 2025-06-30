// <copyright project="NZCore.Hybrid" file="TransformEntityMapping.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using AOT;
using NZCore.AssetManagement;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Jobs;
using Object = UnityEngine.Object;

namespace NZCore.Hybrid
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct TransformEntityMappingSystem : ISystem
    {
        private TransformEntityMappingSingleton mapping;
       
        private NativeList<int> entitiesToRemove;
        private NativeList<DelayedDestroyRequest> delayedDestroyRequests;
        private delegate void DestroyGameObjects(in NativeArray<UnityObjectRef<GameObject>> requests);
        private ManagedDelegate<DestroyGameObjects> destroyGameObjectsFunction;

        public void OnCreate(ref SystemState state)
        {
            state.CreateSingleton(out mapping);
            state.CreateSingleton<CreateHybridObjectRequestSingleton>();
            
            entitiesToRemove = new NativeList<int>(0, Allocator.Persistent);
            delayedDestroyRequests = new NativeList<DelayedDestroyRequest>(0, Allocator.Persistent);
            
            destroyGameObjectsFunction = new ManagedDelegate<DestroyGameObjects>(Destroy);
        }

        public void OnDestroy(ref SystemState state)
        {
            entitiesToRemove.Dispose();
            delayedDestroyRequests.Dispose();
            mapping.Dispose();
            destroyGameObjectsFunction.Dispose();
            
            state.DisposeSingleton<CreateHybridObjectRequestSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //Debug.Log(entitiesList.Length + " transforms in sync system");
            var deltaTime = SystemAPI.Time.DeltaTime;
            var assetLoader = SystemAPI.GetSingleton<WeakAssetLoaderSingleton>();
            
            for (int i = mapping.trackedGameObjects.Length - 1; i >= 0; i--)
            {
                ref var tracked = ref mapping.trackedGameObjects.ElementAt(i);
        
                tracked.DestroyTime -= deltaTime;
        
                if (tracked.DestroyTime <= 0)
                {
                    mapping.destroyRequests.Add(tracked.Object);
                    assetLoader.UnregisterGeneric(tracked.Prefab);
        
                    mapping.trackedGameObjects.RemoveAt(i);
                }
            }
            
            foreach (var index in entitiesToRemove)
            {
                if (index >= mapping.entitiesList.Length)
                    continue;

                var trackedEntity = mapping.entitiesList[index];

                if (trackedEntity.DestroyHybridWithEntity == 1)
                {
                    if (trackedEntity.DestroyDelay > 0)
                    {
                        delayedDestroyRequests.Add(new DelayedDestroyRequest()
                        {
                            Entity = trackedEntity.Entity,
                            Object = trackedEntity.Instance,
                            Time = trackedEntity.DestroyDelay
                        });
                    }
                    else
                    {
                        //Debug.Log($"TransformEntityMapping - Destroying {tmpTransform.gameObject.name}");

                        assetLoader.UnregisterEntity(trackedEntity.Entity);
                        mapping.destroyRequests.Add(trackedEntity.Instance);
                    }
                }

                mapping.RemoveEntity(index, trackedEntity.Entity);
            }

            for (var i = delayedDestroyRequests.Length - 1; i >= 0; i--)
            {
                ref var delayedDestroyRequest = ref delayedDestroyRequests.ElementAt(i);

                delayedDestroyRequest.Time -= deltaTime;

                if (delayedDestroyRequest.Time <= 0)
                {
                    assetLoader.UnregisterEntity(delayedDestroyRequest.Entity);
                    mapping.destroyRequests.Add(delayedDestroyRequest.Object);
                    delayedDestroyRequests.RemoveAt(i);
                }
            }
            
            if (mapping.destroyRequests.Length > 0)
            {
                destroyGameObjectsFunction.Ptr.Invoke(mapping.destroyRequests.AsArray());
                
                mapping.destroyRequests.Clear();
            }

            entitiesToRemove.Clear();

            if (mapping.entitiesList.Capacity > entitiesToRemove.Capacity)
            {
                entitiesToRemove.Capacity = mapping.entitiesList.Capacity;
            }

            state.Dependency = new SyncPositionToEntityJob
            {
                Entities = mapping.entitiesList.AsArray(),
                EntitiesToRemove = entitiesToRemove, // use normal NativeList because the job is scheduled only
                LocalToWorld_Lookup = SystemAPI.GetComponentLookup<LocalToWorld>(true)
            }.Schedule(mapping.transformArray, state.Dependency);
        }

        public void Unload()
        {
            mapping.Clear();
            entitiesToRemove.Clear();
            delayedDestroyRequests.Clear();
        }

        [BurstCompile]
        private unsafe struct SyncPositionToEntityJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<TrackedHybridEntity> Entities;
            [NativeDisableParallelForRestriction] public NativeList<int> EntitiesToRemove;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorld_Lookup;

            public void Execute(int index, TransformAccess transform)
            {
                var entity = Entities[index].Entity;
                if (LocalToWorld_Lookup.TryGetComponentPtrRO(entity, out var localToWorld))
                {
                    //Debug.Log($"Setting positon on {entity} to {localToWorld->Position}");
                    transform.SetPositionAndRotation(localToWorld->Position, localToWorld->Rotation);
                }
                else
                {
                    //Debug.Log($"Queuing destroying hybrid entity on {entity}");
                    EntitiesToRemove.AddNoResize(index);
                }
            }
        }
        
        [MonoPInvokeCallback(typeof(DestroyGameObjects))]
        public static void Destroy(in NativeArray<UnityObjectRef<GameObject>> requests)
        {
            foreach (var request in requests)
            {
                Object.Destroy(request.Value);
            }
        }
    }
}