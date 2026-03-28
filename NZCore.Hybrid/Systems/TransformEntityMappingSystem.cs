// <copyright project="NZCore.Hybrid" file="TransformEntityMapping.cs" version="1.0.0">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
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
        private TransformEntityMappingSingleton _mapping;

        private NativeList<int> _entitiesToRemove;
        private NativeList<DelayedDestroyRequest> _delayedDestroyRequests;

        private delegate void DestroyGameObjects(in NativeArray<UnityObjectRef<GameObject>> requests);

        private ManagedDelegate<DestroyGameObjects> _destroyGameObjectsFunction;

        public void OnCreate(ref SystemState state)
        {
            state.CreateSingleton(out _mapping);
            state.CreateSingleton<CreateHybridObjectRequestSingleton>();

            _entitiesToRemove = new NativeList<int>(0, Allocator.Persistent);
            _delayedDestroyRequests = new NativeList<DelayedDestroyRequest>(0, Allocator.Persistent);

            _destroyGameObjectsFunction = new ManagedDelegate<DestroyGameObjects>(Destroy);
        }

        public void OnDestroy(ref SystemState state)
        {
            _entitiesToRemove.Dispose();
            _delayedDestroyRequests.Dispose();
            _mapping.Dispose();
            _destroyGameObjectsFunction.Dispose();

            state.DisposeSingleton<CreateHybridObjectRequestSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //Debug.Log(entitiesList.Length + " transforms in sync system");
            var deltaTime = SystemAPI.Time.DeltaTime;
            var assetLoader = SystemAPI.GetSingleton<WeakAssetLoaderSingleton>();

            for (var i = _mapping.TrackedGameObjects.Length - 1; i >= 0; i--)
            {
                ref var tracked = ref _mapping.TrackedGameObjects.ElementAt(i);

                tracked.DestroyTime -= deltaTime;

                if (tracked.DestroyTime <= 0)
                {
                    _mapping.DestroyRequests.Add(tracked.Object);
                    assetLoader.UnregisterGeneric(tracked.Prefab);

                    _mapping.TrackedGameObjects.RemoveAt(i);
                }
            }

            foreach (var index in _entitiesToRemove)
            {
                if (index >= _mapping.EntitiesList.Length)
                {
                    continue;
                }

                var trackedEntity = _mapping.EntitiesList[index];

                if (trackedEntity.DestroyHybridWithEntity == 1)
                {
                    if (trackedEntity.DestroyDelay > 0)
                    {
                        _delayedDestroyRequests.Add(new DelayedDestroyRequest
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
                        _mapping.DestroyRequests.Add(trackedEntity.Instance);
                    }
                }

                _mapping.RemoveEntity(index, trackedEntity.Entity);
            }

            for (var i = _delayedDestroyRequests.Length - 1; i >= 0; i--)
            {
                ref var delayedDestroyRequest = ref _delayedDestroyRequests.ElementAt(i);

                delayedDestroyRequest.Time -= deltaTime;

                if (delayedDestroyRequest.Time <= 0)
                {
                    assetLoader.UnregisterEntity(delayedDestroyRequest.Entity);
                    _mapping.DestroyRequests.Add(delayedDestroyRequest.Object);
                    _delayedDestroyRequests.RemoveAt(i);
                }
            }

            if (_mapping.DestroyRequests.Length > 0)
            {
                _destroyGameObjectsFunction.Ptr.Invoke(_mapping.DestroyRequests.AsArray());

                _mapping.DestroyRequests.Clear();
            }

            _entitiesToRemove.Clear();

            if (_mapping.EntitiesList.Capacity > _entitiesToRemove.Capacity)
            {
                _entitiesToRemove.Capacity = _mapping.EntitiesList.Capacity;
            }

            state.Dependency = new SyncPositionToEntityJob
            {
                Entities = _mapping.EntitiesList.AsArray(),
                EntitiesToRemove = _entitiesToRemove, // use normal NativeList because the job is scheduled only
                LocalToWorld_Lookup = SystemAPI.GetComponentLookup<LocalToWorld>(true)
            }.Schedule(_mapping.TransformArray, state.Dependency);
        }

        public void Unload()
        {
            _mapping.Clear();
            _entitiesToRemove.Clear();
            _delayedDestroyRequests.Clear();
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
                else if (!LocalToWorld_Lookup.EntityExists(entity))
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