// <copyright project="NZCore.UI" file="LoadAddressableInterface.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000
using System;
using System.Collections.Generic;
using NZCore.Hybrid;
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
        public bool printLoadedAssets;

        private AddressablesAndHandles<VisualTreeAsset> visualTreeAssets;
        private AddressablesAndHandles<GameObject> worldInterfaceAssets;
        private AddressablesAndHandles<SpriteAtlas> spriteAtlas;
        
        public List<CustomUIAsset> CustomAssets = new();

        public async void Start()
        {
            if (LoadAddressables)
            {
                visualTreeAssets = await AddressableHelper.LoadAssetsFromLabel<VisualTreeAsset>("interface");
                worldInterfaceAssets = await AddressableHelper.LoadAssetsFromLabel<GameObject>("worldInterface");
                spriteAtlas = await AddressableHelper.LoadAssetsFromLabel<SpriteAtlas>("spriteAtlas");
            }
            else
            {
                visualTreeAssets = new AddressablesAndHandles<VisualTreeAsset>();
                worldInterfaceAssets = new AddressablesAndHandles<GameObject>();
                spriteAtlas = new AddressablesAndHandles<SpriteAtlas>();
            }

            if (printLoadedAssets)
            {
                foreach (var asset in visualTreeAssets.Assets)
                {
                    Debug.Log($"{asset.Key}");
                }
            }

            var uiAssets = new UIAssetsSingleton()
            {
                VisualTreeAssets = visualTreeAssets.Assets,
                SpriteAtlasAssets = spriteAtlas.Assets,
                WorldInterfaceAssets = worldInterfaceAssets.Assets
            };
            
            foreach (var customAsset in CustomAssets)
            {
                uiAssets.VisualTreeAssets.Add(customAsset.Key, customAsset.Asset);
            }

            var manager = GetComponent<UIToolkitManager>();
            manager.Assets = uiAssets;
            
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            var entity = em.CreateEntity();
            em.AddComponentObject(entity, uiAssets);
            em.AddComponent<UIAssetsLoaded>(entity);

            // todo, enable UI component group to reduce RequireForUpdate
        }

        private void OnDestroy()
        {
            visualTreeAssets?.UnloadAll();
            spriteAtlas?.UnloadAll();
            worldInterfaceAssets?.UnloadAll();
        }
    }
    
    [Serializable]
    public class CustomUIAsset
    {
        public string Key;
        public VisualTreeAsset Asset;
    }
}
#endif