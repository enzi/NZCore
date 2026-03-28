// <copyright project="NZCore.Hybrid" file="LoadAddressablesSystem.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace NZCore.Hybrid
{
    public struct LoadAddressableLabel : IComponentData
    {
        public FixedString512Bytes Label;
    }

    [UpdateInGroup(typeof(NZCoreInitializationSystemGroup))]
    public partial class LoadAddressablesSystem : SystemBase
    {
        public Dictionary<Hash128, AddressableAndHandle<GameObject>> Addressables;

        protected override void OnCreate()
        {
            Addressables = new Dictionary<Hash128, AddressableAndHandle<GameObject>>();
        }

        protected override void OnUpdate() { }

        protected override void OnDestroy()
        {
            UnloadAddressables();
        }

        public bool RequestLoad(Hash128 key, out GameObject prefab)
        {
            if (Addressables.TryGetValue(key, out var element))
            {
                if (element.Handle.IsDone)
                {
                    if (element.Asset == null)
                    {
                        element.Asset = element.Handle.Result;
                    }

                    prefab = element.Asset;
                    return true;
                }
            }
            else
            {
                //Debug.Log($"Loading Addressable {key.ToString()}");
                var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(key.ToString());

                Addressables.Add(key, new AddressableAndHandle<GameObject>
                {
                    Handle = handle
                });
            }

            prefab = null;
            return false;
        }

        public void UnloadAddressables()
        {
            foreach (var pair in Addressables)
            {
                pair.Value.Unload();
            }

            Addressables.Clear();
        }
    }
}