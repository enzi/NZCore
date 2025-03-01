// <copyright project="NZCore.Hybrid" file="TransformEntityMapping.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.AssetManagement;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Jobs;

namespace NZCore.Hybrid
{
    public struct TrackedHybridEntity
    {
        public Entity Entity;
        public float DestroyDelay;
        public byte DestroyHybridWithEntity;
    }

    public struct DelayedDestroyRequest
    {
        public Entity Entity;
        public UnityObjectRef<GameObject> Object;
        public float Time;
    }

    public struct HybridComponents
    {
        public UnityObjectRef<Animator> Animator;
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TransformEntityMapping : SystemBase
    {
        private NativeList<HybridComponents> hybridComponents;

        private TransformAccessArray transformArray;
        private NativeHashMap<Entity, int> indexLookup;
        private NativeList<TrackedHybridEntity> entitiesList;
        private NativeList<int> entitiesToRemove;
        private NativeList<DelayedDestroyRequest> delayedDestroyRequests;

        protected override void OnCreate()
        {
            hybridComponents = new NativeList<HybridComponents>(0, Allocator.Persistent);
            indexLookup = new NativeHashMap<Entity, int>(0, Allocator.Persistent);

            transformArray = new TransformAccessArray(0);
            entitiesList = new NativeList<TrackedHybridEntity>(0, Allocator.Persistent);
            entitiesToRemove = new NativeList<int>(0, Allocator.Persistent);
            delayedDestroyRequests = new NativeList<DelayedDestroyRequest>(0, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            transformArray.Dispose();
            entitiesList.Dispose();
            entitiesToRemove.Dispose();
            indexLookup.Dispose();
            hybridComponents.Dispose();
            delayedDestroyRequests.Dispose();
        }

        protected override void OnUpdate()
        {
            //Debug.Log(entitiesList.Length + " transforms in sync system");
            var deltaTime = SystemAPI.Time.DeltaTime;
            var assetLoader = SystemAPI.GetSingleton<WeakAssetLoaderSingleton>();

            foreach (var index in entitiesToRemove)
            {
                if (index >= entitiesList.Length)
                    continue;

                var trackedEntity = entitiesList[index];

                if (trackedEntity.DestroyHybridWithEntity == 1)
                {
                    var tmpTransform = transformArray[index];

                    if (trackedEntity.DestroyDelay > 0)
                    {
                        delayedDestroyRequests.Add(new DelayedDestroyRequest()
                        {
                            Entity = trackedEntity.Entity,
                            Object = tmpTransform.gameObject
                        });
                    }
                    else
                    {
                        //Debug.Log($"TransformEntityMapping - Destroying {tmpTransform.gameObject.name}");

                        assetLoader.UnregisterEntity(trackedEntity.Entity);
                        Object.Destroy(tmpTransform.gameObject);
                    }
                }

                RemoveEntity(index, trackedEntity.Entity);
            }

            for (var i = delayedDestroyRequests.Length - 1; i >= 0; i--)
            {
                ref var delayedDestroyRequest = ref delayedDestroyRequests.ElementAt(i);

                delayedDestroyRequest.Time -= deltaTime;

                if (delayedDestroyRequest.Time <= 0)
                {
                    assetLoader.UnregisterEntity(delayedDestroyRequest.Entity);
                    Object.Destroy(delayedDestroyRequest.Object);

                    delayedDestroyRequests.RemoveAt(i);
                }
            }

            entitiesToRemove.Clear();

            Dependency = new SyncPositionToEntityJob
            {
                Entities = entitiesList.AsArray(),
                EntitiesToRemove = entitiesToRemove, // use normal NativeList because the job is scheduled only
                LocalToWorld_Lookup = SystemAPI.GetComponentLookup<LocalToWorld>(true)
            }.Schedule(transformArray, Dependency);
        }

        public bool AddTransform(Transform transform, Entity entity, bool destroyHybridWithEntity = true, float destroyDelay = 0)
        {
            //Debug.Log($"Add mapping for entity {entity} with transform {transform.name}");

            if (indexLookup.TryGetValue(entity, out var index))
            {
                // the entity is already tracked
                // so just parent it

                transform.SetParent(transformArray[index]);
                transform.localPosition = Vector3.zero;

                return true;
            }
            else
            {
                transformArray.Add(transform);
                indexLookup.Add(entity, entitiesList.Length);
                entitiesList.Add(new TrackedHybridEntity() { Entity = entity, DestroyHybridWithEntity = destroyHybridWithEntity.ToByte(), DestroyDelay = destroyDelay });

                hybridComponents.Add(new HybridComponents()
                {
                    Animator = transform.GetComponent<Animator>()
                });

                entitiesToRemove.Capacity = entitiesList.Capacity;

                return false;
            }
        }

        public void RemoveEntity(Entity entity)
        {
            if (indexLookup.TryGetValue(entity, out var index))
            {
                RemoveEntity(index, entity);
            }
        }

        private void RemoveEntity(int index, Entity entity)
        {
            transformArray.RemoveAtSwapBack(index);
            entitiesList.RemoveAtSwapBack(index);
            hybridComponents.RemoveAtSwapBack(index);
            indexLookup.Remove(entity);

            if (index < entitiesList.Length)
            {
                var entityToFix = entitiesList[index].Entity;
                //Debug.Log($"fixing entity {entityToFix} with index {index}");
                indexLookup[entityToFix] = index;
            }
        }
        
        public bool TryGetId(Entity entity, out int index)
        {
            return indexLookup.TryGetValue(entity, out index);
        }

        public bool TryGetTransform(Entity entity, out Transform transform)
        {
            if (indexLookup.TryGetValue(entity, out var index))
            {
                transform = transformArray[index];
                return true;
            }

            transform = null;
            return false;
        }

        public bool TryGetAnimator(Entity entity, out Animator animator)
        {
            if (indexLookup.TryGetValue(entity, out var index))
            {
                animator = hybridComponents[index].Animator;
                return true;
            }

            animator = null;
            return false;
        }

        public void Unload()
        {
            for (int i = transformArray.length - 1; i >= 0; i--)
                transformArray.RemoveAtSwapBack(i);
            indexLookup.Clear();
            entitiesList.Clear();
            hybridComponents.Clear();
            entitiesToRemove.Clear();
            hybridComponents.Clear();
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
                if (LocalToWorld_Lookup.TryGetComponentPtrRO(Entities[index].Entity, out var localToWorld))
                {
                    //Debug.Log($"Setting positon on {entity} to {position.Value}");
                    transform.SetPositionAndRotation(localToWorld->Position, localToWorld->Rotation);
                }
                else
                {
                    //Debug.Log($"Queuing destroying hybrid entity on {Entities[index].Entity}");
                    EntitiesToRemove.AddNoResize(index);
                }
            }
        }
    }
}