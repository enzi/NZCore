// <copyright project="NZCore.Hybrid.Data" file="TransformEntityMappingSingleton.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.Interfaces;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Content;
using UnityEngine;
using UnityEngine.Jobs;

namespace NZCore.Hybrid
{
    public struct TrackedHybridEntity
    {
        public UnityObjectRef<GameObject> Instance;
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
    
    public struct TrackedGameObject
    {
        public WeakObjectReference<GameObject> Prefab;
        public UnityObjectRef<GameObject> Object;
        public float DestroyTime;
    }
    
    public struct TransformEntityMappingSingleton : IInitSingleton, IDisposable
    {
        public TransformAccessArray transformArray;
        public NativeHashMap<Entity, int> indexLookup;
        public NativeList<TrackedHybridEntity> entitiesList;
        public NativeList<HybridComponents> hybridComponents;
        
        public NativeList<UnityObjectRef<GameObject>> destroyRequests;
        public NativeList<TrackedGameObject> trackedGameObjects;

        public void Init()
        {
            hybridComponents = new NativeList<HybridComponents>(0, Allocator.Persistent);
            indexLookup = new NativeHashMap<Entity, int>(0, Allocator.Persistent);

            transformArray = new TransformAccessArray(0);
            entitiesList = new NativeList<TrackedHybridEntity>(0, Allocator.Persistent);
            trackedGameObjects = new NativeList<TrackedGameObject>(0, Allocator.Persistent);
            destroyRequests = new NativeList<UnityObjectRef<GameObject>>(0, Allocator.Persistent);
        }

        public void Dispose()
        {
            transformArray.Dispose();
            entitiesList.Dispose();
            indexLookup.Dispose();
            hybridComponents.Dispose();
            
            trackedGameObjects.Dispose();
            destroyRequests.Dispose();
        }
        
        /// <summary>
        /// Link a new Transform an entity
        /// </summary>
        /// <returns>Returns parented bool</returns>
        public bool AddTransform(
            int instanceId, 
            Entity entity, 
            UnityObjectRef<GameObject> instance,
            UnityObjectRef<Animator> animator = default,
            bool destroyHybridWithEntity = true,
            float destroyDelay = 0)
        {
            //Debug.Log($"Add mapping for entity {entity} with transform {instanceId}");
            
            if (indexLookup.TryGetValue(entity, out var index))
            {
                // the entity is already tracked
                // so just parent it

                // path not possible yet
                // transform.SetParent(transformArray[index]);
                // transform.localPosition = Vector3.zero;

                return true;
            }

            transformArray.Add(instanceId);
            indexLookup.Add(entity, entitiesList.Length);
            entitiesList.Add(new TrackedHybridEntity()
            {
                Instance = instance,
                Entity = entity, 
                DestroyHybridWithEntity = destroyHybridWithEntity.ToByte(), 
                DestroyDelay = destroyDelay
            });

            hybridComponents.Add(new HybridComponents()
            {
                Animator = animator
            });

            return false;
        }
        
        public void RemoveEntity(Entity entity)
        {
            if (indexLookup.TryGetValue(entity, out var index))
            {
                RemoveEntity(index, entity);
            }
        }

        public void RemoveEntity(int index, Entity entity)
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

        public void Clear()
        {
            for (int i = transformArray.length - 1; i >= 0; i--)
            {
                transformArray.RemoveAtSwapBack(i);
            }

            indexLookup.Clear();
            entitiesList.Clear();
            hybridComponents.Clear();
            hybridComponents.Clear();
            trackedGameObjects.Clear();
        }
    }
}