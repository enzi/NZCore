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
        public TransformAccessArray TransformArray;
        public NativeHashMap<Entity, int> IndexLookup;
        public NativeList<TrackedHybridEntity> EntitiesList;
        public NativeList<HybridComponents> HybridComponents;
        
        public NativeList<UnityObjectRef<GameObject>> DestroyRequests;
        public NativeList<TrackedGameObject> TrackedGameObjects;

        public void Init()
        {
            HybridComponents = new NativeList<HybridComponents>(0, Allocator.Persistent);
            IndexLookup = new NativeHashMap<Entity, int>(0, Allocator.Persistent);

            TransformArray = new TransformAccessArray(0);
            EntitiesList = new NativeList<TrackedHybridEntity>(0, Allocator.Persistent);
            TrackedGameObjects = new NativeList<TrackedGameObject>(0, Allocator.Persistent);
            DestroyRequests = new NativeList<UnityObjectRef<GameObject>>(0, Allocator.Persistent);
        }

        public void Dispose()
        {
            TransformArray.Dispose();
            EntitiesList.Dispose();
            IndexLookup.Dispose();
            HybridComponents.Dispose();
            
            TrackedGameObjects.Dispose();
            DestroyRequests.Dispose();
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
            
            if (IndexLookup.TryGetValue(entity, out var index))
            {
                // the entity is already tracked
                // so just parent it

                // path not possible yet
                // transform.SetParent(transformArray[index]);
                // transform.localPosition = Vector3.zero;

                return true;
            }

            TransformArray.Add(instanceId);
            IndexLookup.Add(entity, EntitiesList.Length);
            EntitiesList.Add(new TrackedHybridEntity()
            {
                Instance = instance,
                Entity = entity, 
                DestroyHybridWithEntity = destroyHybridWithEntity.ToByte(), 
                DestroyDelay = destroyDelay
            });

            HybridComponents.Add(new HybridComponents()
            {
                Animator = animator
            });

            return false;
        }
        
        public void RemoveEntity(Entity entity)
        {
            if (IndexLookup.TryGetValue(entity, out var index))
            {
                RemoveEntity(index, entity);
            }
        }

        public void RemoveEntity(int index, Entity entity)
        {
            TransformArray.RemoveAtSwapBack(index);
            EntitiesList.RemoveAtSwapBack(index);
            HybridComponents.RemoveAtSwapBack(index);
            IndexLookup.Remove(entity);

            if (index < EntitiesList.Length)
            {
                var entityToFix = EntitiesList[index].Entity;
                //Debug.Log($"fixing entity {entityToFix} with index {index}");
                IndexLookup[entityToFix] = index;
            }
        }
        
        public bool TryGetId(Entity entity, out int index)
        {
            return IndexLookup.TryGetValue(entity, out index);
        }

        public bool TryGetTransform(Entity entity, out Transform transform)
        {
            if (IndexLookup.TryGetValue(entity, out var index))
            {
                transform = TransformArray[index];
                return true;
            }

            transform = null;
            return false;
        }

        public bool TryGetAnimator(Entity entity, out Animator animator)
        {
            if (IndexLookup.TryGetValue(entity, out var index))
            {
                animator = HybridComponents[index].Animator;
                return true;
            }

            animator = null;
            return false;
        }

        public void Clear()
        {
            for (int i = TransformArray.length - 1; i >= 0; i--)
            {
                TransformArray.RemoveAtSwapBack(i);
            }

            IndexLookup.Clear();
            EntitiesList.Clear();
            HybridComponents.Clear();
            HybridComponents.Clear();
            TrackedGameObjects.Clear();
        }
    }
}