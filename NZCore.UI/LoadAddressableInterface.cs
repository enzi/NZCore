// <copyright project="NZCore" file="LoadAddressableInterface.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

#if UNITY_6000
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
        public bool printLoadedAssets;

        private AddressablesAndHandles<VisualTreeAsset> visualTreeAssets;
        private AddressablesAndHandles<GameObject> worldInterfaceAssets;
        private AddressablesAndHandles<SpriteAtlas> spriteAtlas;

        public async void Start()
        {
            visualTreeAssets = await AddressablesHelper.LoadAssetsFromLabel<VisualTreeAsset>("interface");
            worldInterfaceAssets = await AddressablesHelper.LoadAssetsFromLabel<GameObject>("worldInterface");
            spriteAtlas = await AddressablesHelper.LoadAssetsFromLabel<SpriteAtlas>("spriteAtlas");

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
            visualTreeAssets.UnloadAll();
            spriteAtlas.UnloadAll();
            worldInterfaceAssets.UnloadAll();
        }
    }
}
#endif