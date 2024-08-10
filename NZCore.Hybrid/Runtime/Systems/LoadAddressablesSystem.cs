// <copyright project="NZCore" file="LoadAddressablesSystem.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Hash128 = Unity.Entities.Hash128;

namespace NZCore.Hybrid
{
    public struct LoadAddressableLabel : IComponentData
    {
        public FixedString512Bytes label;
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class LoadAddressablesSystem : SystemBase
    {
        public Dictionary<Hash128, AddressableAndHandle<GameObject>> addressables;

        protected override void OnCreate()
        {
            addressables = new Dictionary<Hash128, AddressableAndHandle<GameObject>>();
        }

        protected override void OnUpdate()
        {
        }

        protected override void OnDestroy()
        {
            UnloadAddressables();
        }

        public bool RequestLoad(Hash128 key, out GameObject prefab)
        {
            if (addressables.ContainsKey(key))
            {
                var tmp = addressables[key];
                if (tmp.Handle.IsDone)
                {
                    if (tmp.Asset == null)
                        tmp.Asset = tmp.Handle.Result;

                    prefab = tmp.Asset;
                    return true;
                }
            }
            else
            {
                //Debug.Log($"Loading Addressable {key.ToString()}");
                var handle = Addressables.LoadAssetAsync<GameObject>(key.ToString());

                addressables.Add(key, new AddressableAndHandle<GameObject>()
                {
                    Handle = handle
                });
            }

            prefab = null;
            return false;
        }

        public void UnloadAddressables()
        {
            foreach (var pair in addressables)
            {
                pair.Value.Unload();
            }

            addressables.Clear();
        }
    }
}