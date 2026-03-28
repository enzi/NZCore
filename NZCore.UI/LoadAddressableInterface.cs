// <copyright project="NZCore.UI" file="LoadAddressableInterface.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000 && !NZCORE_MVVM
using System.Collections.Generic;
using NZCore.Hybrid;
using NZCore.UI;
using Unity.Entities;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    [RequireComponent(typeof(UIToolkitManager))]
    public class LoadAddressableInterface : MonoBehaviour
    {
        public bool LoadAddressables = true;
        public bool PrintLoadedAssets;

        private AddressablesAndHandles<VisualTreeAsset> _visualTreeAssets;
        private AddressablesAndHandles<GameObject> _worldInterfaceAssets;
        private AddressablesAndHandles<SpriteAtlas> _spriteAtlas;

        public List<CustomUIAsset> CustomAssets = new();

        public async void Start()
        {
            if (LoadAddressables)
            {
                _visualTreeAssets = await AddressableHelper.LoadAssetsFromLabel<VisualTreeAsset>("interface");
                _worldInterfaceAssets = await AddressableHelper.LoadAssetsFromLabel<GameObject>("worldInterface");
                _spriteAtlas = await AddressableHelper.LoadAssetsFromLabel<SpriteAtlas>("spriteAtlas");
            }
            else
            {
                _visualTreeAssets = new AddressablesAndHandles<VisualTreeAsset>();
                _worldInterfaceAssets = new AddressablesAndHandles<GameObject>();
                _spriteAtlas = new AddressablesAndHandles<SpriteAtlas>();
            }

            if (PrintLoadedAssets)
            {
                foreach (var asset in _visualTreeAssets.Assets)
                {
                    Debug.Log($"{asset.Key}");
                }
            }
            

            var uiAssets = UIToolkitManager.Instance.Assets;

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

            foreach (var customAsset in CustomAssets)
            {
                uiAssets.VisualTreeAssets.Add(customAsset.Key, customAsset.Asset);
            }

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            var entity = em.CreateEntity();
            em.AddComponentObject(entity, uiAssets);
            em.AddComponent<UIAssetsLoaded>(entity);

            // todo, enable UI component group to reduce RequireForUpdate
        }

        private void OnDestroy()
        {
            _visualTreeAssets?.UnloadAll();
            _spriteAtlas?.UnloadAll();
            _worldInterfaceAssets?.UnloadAll();
        }
    }
}
#endif