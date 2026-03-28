// <copyright project="NZCore" file="WeakAssetLoaderSystem.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
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

        private float _unloadTime;

        private NativeHashMap<Hash128, AssetDependency> _assetDependencyMap;
        private NativeParallelMultiHashMap<Entity, UntypedWeakReferenceId> _entityToAssetsMap;

        private NativeHashSet<Hash128> _requestedAssets;
        private NativeList<UntypedWeakReferenceId> _loadRequests;
        private NativeList<AssetUnloadRequest> _unloadRequests;

        internal void Init(float timeToUnload)
        {
            _unloadTime = timeToUnload;

            _assetDependencyMap = new NativeHashMap<Hash128, AssetDependency>(0, Allocator.Persistent);
            _requestedAssets = new NativeHashSet<Hash128>(0, Allocator.Persistent);
            _entityToAssetsMap = new NativeParallelMultiHashMap<Entity, UntypedWeakReferenceId>(0, Allocator.Persistent);
            _loadRequests = new NativeList<UntypedWeakReferenceId>(0, Allocator.Persistent);
            _unloadRequests = new NativeList<AssetUnloadRequest>(0, Allocator.Persistent);
        }

        internal void Dispose()
        {
            //Debug.Log("WeakAssetLoaderSingleton Dispose!");
            // ReSharper disable once NotDisposedResource
            var enumerator = _assetDependencyMap.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var tmp = enumerator.Current;
                Debug.Log($"Found {tmp.Value.UntypedAssetId.GlobalId.AssetGUID} that was not unloaded correctly!");
                RuntimeContentManager.ReleaseObjectAsync(tmp.Value.UntypedAssetId);
            }

            // todo: cancel loadAsyncs in flight?

            _assetDependencyMap.Dispose();
            _requestedAssets.Dispose();
            _entityToAssetsMap.Dispose();
            _loadRequests.Dispose();
            _unloadRequests.Dispose();
        }

        internal void Update(float deltaTime)
        {
            for (var i = _loadRequests.Length - 1; i >= 0; i--)
            {
                ref var req = ref _loadRequests.ElementAt(i);
                var loadStatus = RuntimeContentManager.GetObjectLoadingStatus(req);
                var hash = req.GlobalId.AssetGUID;

                switch (loadStatus)
                {
                    case ObjectLoadingStatus.None:
                    case ObjectLoadingStatus.Queued:
                    case ObjectLoadingStatus.Loading:
                        break;
                    case ObjectLoadingStatus.Completed:
                        var assetDependency = new AssetDependency
                        {
                            UntypedAssetId = req
                        };

                        //Debug.Log($"Adding {hash} to asset dependency map!");
                        _assetDependencyMap.Add(hash, assetDependency);
                        _loadRequests.RemoveAt(i);
                        break;
                    case ObjectLoadingStatus.Error:
                        _requestedAssets.Remove(hash);
                        _loadRequests.RemoveAt(i);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            for (var i = _unloadRequests.Length - 1; i >= 0; i--)
            {
                ref var req = ref _unloadRequests.ElementAt(i);

                var hash = req.UntypedAssetId.GlobalId.AssetGUID;

                if (_assetDependencyMap.TryGetValue(hash, out var assetDependency) && assetDependency.DependencyCount > 0)
                {
                    //Debug.Log($"Canceled unload request for {hash}");
                    // cancel request, another entity depends on it
                    _unloadRequests.RemoveAt(i);
                    continue;
                }

                req.UnloadTime -= deltaTime;
                //Debug.Log($"Unload req {hash} with time: {req.UnloadTime} ");

                if (req.UnloadTime <= 0)
                {
                    //Debug.Log($"Unloading {hash} with time {req.UnloadTime}");

                    _requestedAssets.Remove(hash);
                    _assetDependencyMap.Remove(hash);

                    RuntimeContentManager.ReleaseObjectAsync(req.UntypedAssetId);

                    _unloadRequests.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Call this first to load a specific asset
        /// </summary>
        /// <returns>Returns false on invalid reference</returns>
        public bool Load(UntypedWeakReferenceId untypedWeakReference)
        {
            if (!untypedWeakReference.IsValid)
            {
                return false;
            }

            var hash = untypedWeakReference.GlobalId.AssetGUID;

            if (_requestedAssets.Contains(hash))
            {
                return true;
            }

            //Debug.Log($"Loading {hash}");
            RuntimeContentManager.LoadObjectAsync(untypedWeakReference);

            _requestedAssets.Add(hash);
            _loadRequests.Add(untypedWeakReference);

            return true;
        }

        /// <summary>
        /// Call this first to load a specific asset
        /// </summary>
        /// <returns>Returns false on invalid reference</returns>
        public bool Load<T>(WeakObjectReference<T> weakRef)
            where T : Object
        {
            if (!weakRef.IsValidBurst())
            {
                return false;
            }

            var hash = weakRef.Id.GlobalId.AssetGUID;

            if (_requestedAssets.Contains(hash))
            {
                return true;
            }

            //Debug.Log($"Loading {hash}");
            weakRef.LoadAsync();
            _requestedAssets.Add(hash);
            _loadRequests.Add(weakRef.Id);

            return true;
        }

        public bool HasLoaded(UntypedWeakReferenceId untypedWeakReference) =>
            //Debug.Log($"TryGetResult returning {hash} prefab. {weakRef.LoadingStatus}/{weakRef.IsReferenceValid}/{weakRef.Result}");
            _assetDependencyMap.ContainsKey(untypedWeakReference.GlobalId.AssetGUID);

        /// <summary>
        /// After a Load has been called, call this method to get the actual asset object
        /// </summary>
        /// <returns>Returns true when loading has been completed</returns>
        public bool HasLoaded<T>(WeakObjectReference<T> weakRef)
            where T : Object =>
            //Debug.Log($"TryGetResult returning {hash} prefab. {weakRef.LoadingStatus}/{weakRef.IsReferenceValid}/{weakRef.Result}");
            _assetDependencyMap.ContainsKey(weakRef.Id.GlobalId.AssetGUID);

        /// <summary>
        /// After TryGetResult was successful, the entity should be registered as a dependency
        /// for the asset
        /// </summary>
        public unsafe void RegisterEntity<T>(WeakObjectReference<T> weakRef, Entity entity)
            where T : Object
        {
            var hash = weakRef.Id.GlobalId.AssetGUID;

            if (_assetDependencyMap.TryGetRefValue(hash, out var assetDependencyPtr))
            {
                assetDependencyPtr->DependencyCount++;
                _entityToAssetsMap.Add(entity, weakRef.Id);
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

            if (!_assetDependencyMap.TryGetRefValue(hash, out var assetDependencyPtr))
            {
                Debug.LogError($"{hash} has never been loaded!");
                return;
            }

            assetDependencyPtr->DependencyCount--;
            //Debug.Log($"UnregisterEntity {entity} - new dep count: {assetDependencyPtr->DependencyCount}");

            if (assetDependencyPtr->DependencyCount == 0)
            {
                //Debug.Log($"UnregisterEntity new unload request for {hash}");
                _unloadRequests.Add(new AssetUnloadRequest
                {
                    UntypedAssetId = weakRef.Id,
                    UnloadTime = _unloadTime
                });
            }

            if (!_entityToAssetsMap.TryGetFirstValue(entity, out var val, out var it))
            {
                return;
            }

            if (val == weakRef.Id)
            {
                _entityToAssetsMap.Remove(it);
            }
            else
            {
                while (_entityToAssetsMap.TryGetNextValue(out val, ref it))
                {
                    if (val != weakRef.Id)
                    {
                        continue;
                    }

                    _entityToAssetsMap.Remove(it);
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
            var enumerator = _entityToAssetsMap.GetValuesForKey(entity);

            while (enumerator.MoveNext())
            {
                var untypedRef = enumerator.Current;
                var hash = untypedRef.GlobalId.AssetGUID;

                if (!_assetDependencyMap.TryGetRefValue(hash, out var assetDependencyPtr))
                {
                    Debug.Log($"UnregisterEntity {entity} - {hash} not found!");
                    continue;
                }

                assetDependencyPtr->DependencyCount--;

                //Debug.Log($"UnregisterEntity {entity} - new dep count: {assetDependencyPtr->DependencyCount}");

                if (assetDependencyPtr->DependencyCount == 0)
                {
                    //Debug.Log($"UnregisterEntity new unload request for {hash}");
                    _unloadRequests.Add(new AssetUnloadRequest
                    {
                        UntypedAssetId = untypedRef,
                        UnloadTime = _unloadTime
                    });
                }
            }

            _entityToAssetsMap.Remove(entity);
        }

        public unsafe void RegisterGeneric<T>(WeakObjectReference<T> weakRef)
            where T : Object
        {
            var hash = weakRef.Id.GlobalId.AssetGUID;

            if (_assetDependencyMap.TryGetRefValue(hash, out var assetDependencyPtr))
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

            if (!_assetDependencyMap.TryGetRefValue(hash, out var assetDependencyPtr))
            {
                Debug.LogError($"{hash} has never been loaded!");
                return;
            }

            assetDependencyPtr->DependencyCount--;

            if (assetDependencyPtr->DependencyCount == 0)
            {
                //Debug.Log($"UnregisterGeneric new unload request for {hash}");
                _unloadRequests.Add(new AssetUnloadRequest
                {
                    UntypedAssetId = weakRef.Id,
                    UnloadTime = _unloadTime
                });
            }
        }
    }

    [UpdateInGroup(typeof(NZCoreInitializationSystemGroup))]
    public partial struct WeakAssetLoaderSystem : ISystem
    {
        private WeakAssetLoaderSingleton _singleton;

        public void OnCreate(ref SystemState state)
        {
            _singleton = new WeakAssetLoaderSingleton();
            _singleton.Init(5.0f);

            var ent = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(ent, _singleton);
        }

        public void OnDestroy(ref SystemState state)
        {
            _singleton.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            _singleton.Update(deltaTime);
        }
    }
}