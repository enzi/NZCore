// <copyright project="NZCore.UI" file="LoadAddressablesSystem.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using NZCore.Hybrid;
using NZCore.UIToolkit.Data;
using Unity.Entities;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class LoadAddressablesSystem : SystemBase
    {
        private AddressablesAndHandles<VisualTreeAsset> _visualTreeAssets;
        private AddressablesAndHandles<GameObject> _worldInterfaceAssets;
        private AddressablesAndHandles<SpriteAtlas> _spriteAtlas;

        protected override void OnCreate()
        {
            RequireForUpdate<LoadAddressablesRequest>();
        }

        protected override void OnUpdate()
        {
            var requestEntity = SystemAPI.GetSingletonEntity<LoadAddressablesRequest>();
            var request = SystemAPI.GetSingleton<LoadAddressablesRequest>();
            EntityManager.DestroyEntity(requestEntity);

            SystemAPI.ManagedAPI.TryGetSingleton(out LoadCustomUIAssetsRequest customAssetsRequest);

            Enabled = false;
            LoadAsync(customAssetsRequest.CustomAssets, request.PrintLoadedAssets);
        }

        private async void LoadAsync(List<CustomUIAsset> customAssets, bool printLoadedAssets)
        {
            _visualTreeAssets = await AddressableHelper.LoadAssetsFromLabel<VisualTreeAsset>("interface");
            _worldInterfaceAssets = await AddressableHelper.LoadAssetsFromLabel<GameObject>("worldInterface");
            _spriteAtlas = await AddressableHelper.LoadAssetsFromLabel<SpriteAtlas>("spriteAtlas");

            if (printLoadedAssets)
            {
                foreach (var asset in _visualTreeAssets.Assets)
                {
                    Debug.Log(asset.Key);
                }
            }

            var uiAssets = new UIAssetsSingleton();

            foreach (var kvp in _visualTreeAssets.Assets)
            {
                uiAssets.VisualTreeAssets.Add(kvp.Key, kvp.Value);
            }

            foreach (var kvp in _spriteAtlas.Assets)
            {
                uiAssets.SpriteAtlasAssets.Add(kvp.Key, kvp.Value);
            }

            foreach (var kvp in _worldInterfaceAssets.Assets)
            {
                uiAssets.WorldInterfaceAssets.Add(kvp.Key, kvp.Value);
            }

            if (customAssets != null)
            {
                foreach (var custom in customAssets)
                {
                    uiAssets.VisualTreeAssets.Add(custom.Key, custom.Asset);
                }
            }

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var entity = em.CreateEntity();
            em.AddComponentObject(entity, uiAssets);
            em.AddComponent<UIAssetsLoaded>(entity);
        }

        protected override void OnDestroy()
        {
            _visualTreeAssets?.UnloadAll();
            _spriteAtlas?.UnloadAll();
            _worldInterfaceAssets?.UnloadAll();
        }
    }
}