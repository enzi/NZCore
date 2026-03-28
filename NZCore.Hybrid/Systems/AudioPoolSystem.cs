// <copyright project="NZCore.Hybrid" file="AudioPoolSystem.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using NZCore.AssetManagement;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Pool;

namespace NZCore.Hybrid
{
    public partial class AudioPoolSystem : SystemBase
    {
        private ObjectPool<AudioSource> _pool;
        private List<ActiveAudioSource> _activeSources;

        protected override void OnCreate()
        {
            _activeSources = new List<ActiveAudioSource>();
            CheckedStateRef.CreateSingleton<HybridAudioSingleton>();

            _pool = new ObjectPool<AudioSource>(() =>
                {
                    var go = new GameObject("AudioSourcePoolItem")
                    {
                        hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
                    };
                    return go.AddComponent<AudioSource>();
                },
                null,
                null,
                source =>
                {
                    if (source != null)
                    {
                        Object.Destroy(source.gameObject);
                    }
                });

            RequireForUpdate<TransformEntityMappingSingleton>();
        }

        protected override void OnDestroy()
        {
            CheckedStateRef.DisposeSingleton<HybridAudioSingleton>();
            _pool.Dispose();
        }

        protected override void OnUpdate()
        {
            EntityManager.CompleteDependencyBeforeRW<HybridAudioSingleton>();

            var assetLoader = SystemAPI.GetSingleton<WeakAssetLoaderSingleton>();
            var singleton = SystemAPI.GetSingleton<HybridAudioSingleton>();
            var enumerator = singleton.Requests.GetEnumerator();
            var mapping = SystemAPI.GetSingleton<TransformEntityMappingSingleton>();

            while (enumerator.MoveNext())
            {
                ref var requestList = ref enumerator.Current;

                for (var i = requestList.Length - 1; i >= 0; i--)
                {
                    var request = requestList[i];
                    if (!assetLoader.Load(request.Clip))
                    {
                        requestList.RemoveAt(i);
                        continue;
                    }

                    if (!assetLoader.HasLoaded(request.Clip))
                    {
                        continue;
                    }

                    var source = _pool.Get();

                    source.clip = request.Clip.Result;
                    source.volume = request.Volume;
                    source.spatialBlend = request.Is3d ? 1 : 0;

                    if (request.Is3d)
                    {
                        source.transform.position = request.Position;
                    }

                    source.Play();

                    var activeSource = new ActiveAudioSource
                    {
                        Source = source,
                        FollowedEntity = request.FollowEntity
                    };

                    _activeSources.Add(activeSource);

                    if (request.FollowEntity != Entity.Null)
                    {
                        activeSource.Parented = mapping.AddTransform(
                            source.gameObject.GetInstanceID(),
                            request.FollowEntity,
                            source.gameObject);
                    }

                    requestList.RemoveAt(i);
                }
            }

            for (var i = _activeSources.Count - 1; i >= 0; i--)
            {
                var source = _activeSources[i];
                if (source.Source.isPlaying)
                {
                    continue;
                }

                if (source.FollowedEntity != Entity.Null)
                {
                    if (source.Parented)
                    {
                        source.Source.transform.SetParent(null);
                    }
                    else
                    {
                        mapping.RemoveEntity(source.FollowedEntity);
                    }
                }

                _pool.Release(source.Source);
                _activeSources.RemoveAt(i);
            }
        }

        private class ActiveAudioSource
        {
            public AudioSource Source;
            public Entity FollowedEntity;
            public bool Parented;
        }
    }
}