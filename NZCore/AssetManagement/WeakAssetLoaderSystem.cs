// <copyright project="NZCore" file="WeakAssetLoaderSystem.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using UnityEngine;
using Hash128 = UnityEngine.Hash128;
using Object = UnityEngine.Object;

namespace NZCore.AssetManagement
{
    public struct WeakAssetLoaderSingleton : IComponentData
    {
        private struct AssetDependency
        {
            public UntypedWeakReferenceId UntypedAssetId;
            public int DependencyCount;
        }

        private struct AssetUnloadRequest
        {
            public UntypedWeakReferenceId UntypedAssetId;
            public float UnloadTime;
        }

        private float unloadTime;

        private NativeHashMap<Hash128, AssetDependency> assetDependencyMap;
        private NativeParallelMultiHashMap<Entity, UntypedWeakReferenceId> entityToAssetsMap;

        private NativeHashSet<Hash128> requestedAssets;
        private NativeList<UntypedWeakReferenceId> loadRequests;
        private NativeList<AssetUnloadRequest> unloadRequests;

        internal void Init(float timeToUnload)
        {
            unloadTime = timeToUnload;

            assetDependencyMap = new NativeHashMap<Hash128, AssetDependency>(0, Allocator.Persistent);
            requestedAssets = new NativeHashSet<Hash128>(0, Allocator.Persistent);
            entityToAssetsMap = new NativeParallelMultiHashMap<Entity, UntypedWeakReferenceId>(0, Allocator.Persistent);
            loadRequests = new NativeList<UntypedWeakReferenceId>(0, Allocator.Persistent);
            unloadRequests = new NativeList<AssetUnloadRequest>(0, Allocator.Persistent);
        }

        internal void Dispose()
        {
            //Debug.Log("WeakAssetLoaderSingleton Dispose!");
            // ReSharper disable once NotDisposedResource
            var enumerator = assetDependencyMap.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var tmp = enumerator.Current;
                Debug.Log($"Found {tmp.Value.UntypedAssetId.GlobalId.AssetGUID} that was not unloaded correctly!");
                RuntimeContentManager.ReleaseObjectAsync(tmp.Value.UntypedAssetId);
            }

            // todo: cancel loadAsyncs in flight?

            assetDependencyMap.Dispose();
            requestedAssets.Dispose();
            entityToAssetsMap.Dispose();
            loadRequests.Dispose();
            unloadRequests.Dispose();
        }

        internal void Update(float deltaTime)
        {
            for (int i = loadRequests.Length - 1; i >= 0; i--)
            {
                ref var req = ref loadRequests.ElementAt(i);
                var loadStatus = RuntimeContentManager.GetObjectLoadingStatus(req);
                var hash = req.GlobalId.AssetGUID;

                switch (loadStatus)
                {
                    case ObjectLoadingStatus.None:
                    case ObjectLoadingStatus.Queued:
                    case ObjectLoadingStatus.Loading:
                        break;
                    case ObjectLoadingStatus.Completed:
                        var assetDependency = new AssetDependency()
                        {
                            UntypedAssetId = req
                        };

                        //Debug.Log($"Adding {hash} to asset dependency map!");
                        assetDependencyMap.Add(hash, assetDependency);
                        loadRequests.RemoveAt(i);
                        break;
                    case ObjectLoadingStatus.Error:
                        requestedAssets.Remove(hash);
                        loadRequests.RemoveAt(i);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            for (int i = unloadRequests.Length - 1; i >= 0; i--)
            {
                ref var req = ref unloadRequests.ElementAt(i);

                var hash = req.UntypedAssetId.GlobalId.AssetGUID;

                if (assetDependencyMap.TryGetValue(hash, out var assetDependency) && assetDependency.DependencyCount > 0)
                {
                    //Debug.Log($"Canceled unload request for {hash}");
                    // cancel request, another entity depends on it
                    unloadRequests.RemoveAt(i);
                    continue;
                }

                req.UnloadTime -= deltaTime;
                //Debug.Log($"Unload req {hash} with time: {req.UnloadTime} ");

                if (req.UnloadTime <= 0)
                {
                    //Debug.Log($"Unloading {hash} with time {req.UnloadTime}");

                    requestedAssets.Remove(hash);
                    assetDependencyMap.Remove(hash);

                    RuntimeContentManager.ReleaseObjectAsync(req.UntypedAssetId);

                    unloadRequests.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Call this first to load a specific asset
        /// </summary>
        /// <returns>Returns false on invalid reference</returns>
        public bool Load<T>(WeakObjectReference<T> weakRef)
            where T : Object
        {
            if (!weakRef.IsReferenceValid)
                return false;

            var hash = weakRef.Id.GlobalId.AssetGUID;

            if (requestedAssets.Contains(hash))
            {
                return true;
            }

            //Debug.Log($"Loading {hash}");
            weakRef.LoadAsync();
            requestedAssets.Add(hash);
            loadRequests.Add(weakRef.Id);

            return true;
        }

        /// <summary>
        /// After a Load has been called, call this method to get the actual asset object
        /// </summary>
        /// <returns>Returns true when loading has been completed</returns>
        public bool TryGetResult<T>(WeakObjectReference<T> weakRef, out T result)
            where T : Object
        {
            var hash = weakRef.Id.GlobalId.AssetGUID;

            if (assetDependencyMap.ContainsKey(hash))
            {
                //Debug.Log($"TryGetResult returning {hash} prefab. {weakRef.LoadingStatus}/{weakRef.IsReferenceValid}/{weakRef.Result}");
                result = weakRef.Result;
                return true;
            }

            result = null;
            return false;
        }

        /// <summary>
        /// After TryGetResult was successful, the entity should be registered as a dependency
        /// for the asset
        /// </summary>
        public unsafe void RegisterEntity<T>(WeakObjectReference<T> weakRef, Entity entity)
            where T : Object
        {
            var hash = weakRef.Id.GlobalId.AssetGUID;

            if (assetDependencyMap.TryGetRefValue(hash, out var assetDependencyPtr))
            {
                assetDependencyPtr->DependencyCount++;
                entityToAssetsMap.Add(entity, weakRef.Id);
                //Debug.Log($"RegisterEntity {entity} - new dep count: {assetDependencyPtr->DependencyCount}");
            }
            else
            {
                Debug.LogError($"{hash} has never been loaded!");
            }
        }

        /// <summary>
        /// Unregister a specific asset and entity
        /// When no dependencies have been found on the asset an UnloadRequest will be made
        /// </summary>
        public unsafe void UnregisterEntity<T>(Entity entity, WeakObjectReference<T> weakRef)
            where T : Object
        {
            var hash = weakRef.Id.GlobalId.AssetGUID;

            if (!assetDependencyMap.TryGetRefValue(hash, out var assetDependencyPtr))
            {
                Debug.LogError($"{hash} has never been loaded!");
                return;
            }

            assetDependencyPtr->DependencyCount--;
            //Debug.Log($"UnregisterEntity {entity} - new dep count: {assetDependencyPtr->DependencyCount}");

            if (assetDependencyPtr->DependencyCount == 0)
            {
                //Debug.Log($"UnregisterEntity new unload request for {hash}");
                unloadRequests.Add(new AssetUnloadRequest
                {
                    UntypedAssetId = weakRef.Id,
                    UnloadTime = unloadTime
                });
            }

            if (!entityToAssetsMap.TryGetFirstValue(entity, out var val, out var it))
                return;

            if (val == weakRef.Id)
                entityToAssetsMap.Remove(it);
            else
            {
                while (entityToAssetsMap.TryGetNextValue(out val, ref it))
                {
                    if (val != weakRef.Id)
                        continue;

                    entityToAssetsMap.Remove(it);
                    break;
                }
            }
        }

        /// <summary>
        /// Unregister an entity with every dependent asset.
        /// When no dependencies are found for an asset an UnloadRequest will be made.
        /// </summary>
        public unsafe void UnregisterEntity(Entity entity)
        {
            var enumerator = entityToAssetsMap.GetValuesForKey(entity);

            while (enumerator.MoveNext())
            {
                var untypedRef = enumerator.Current;
                var hash = untypedRef.GlobalId.AssetGUID;

                if (!assetDependencyMap.TryGetRefValue(hash, out var assetDependencyPtr))
                {
                    Debug.Log($"UnregisterEntity {entity} - {hash} not found!");
                    continue;
                }

                assetDependencyPtr->DependencyCount--;

                //Debug.Log($"UnregisterEntity {entity} - new dep count: {assetDependencyPtr->DependencyCount}");

                if (assetDependencyPtr->DependencyCount == 0)
                {
                    //Debug.Log($"UnregisterEntity new unload request for {hash}");
                    unloadRequests.Add(new AssetUnloadRequest
                    {
                        UntypedAssetId = untypedRef,
                        UnloadTime = unloadTime
                    });
                }
            }

            entityToAssetsMap.Remove(entity);
        }

        public unsafe void RegisterGeneric<T>(WeakObjectReference<T> weakRef)
            where T : Object
        {
            var hash = weakRef.Id.GlobalId.AssetGUID;

            if (assetDependencyMap.TryGetRefValue(hash, out var assetDependencyPtr))
            {
                assetDependencyPtr->DependencyCount++;
                //Debug.Log($"RegisterGeneric new dep count: {assetDependencyPtr->DependencyCount}");
            }
            else
            {
                Debug.LogError($"{hash} has never been loaded!");
            }
        }

        public unsafe void UnregisterGeneric<T>(WeakObjectReference<T> weakRef)
            where T : Object
        {
            var hash = weakRef.Id.GlobalId.AssetGUID;

            if (!assetDependencyMap.TryGetRefValue(hash, out var assetDependencyPtr))
            {
                Debug.LogError($"{hash} has never been loaded!");
                return;
            }

            assetDependencyPtr->DependencyCount--;

            if (assetDependencyPtr->DependencyCount == 0)
            {
                //Debug.Log($"UnregisterGeneric new unload request for {hash}");
                unloadRequests.Add(new AssetUnloadRequest
                {
                    UntypedAssetId = weakRef.Id,
                    UnloadTime = unloadTime
                });
            }
        }
    }

    public partial struct WeakAssetLoaderSystem : ISystem
    {
        private WeakAssetLoaderSingleton singleton;

        public void OnCreate(ref SystemState state)
        {
            singleton = new WeakAssetLoaderSingleton();
            singleton.Init(5.0f);

            var ent = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(ent, singleton);
        }

        public void OnDestroy(ref SystemState state)
        {
            singleton.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            singleton.Update(deltaTime);
        }
    }
}