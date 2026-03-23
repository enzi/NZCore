// <copyright project="NZCore.MVVM" file="VisualAssetStore.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.MVVM
{
    public interface IVisualAssetStore
    {
        VisualTreeAsset GetAsset(string key);
        bool TryGetAsset(string key, out VisualTreeAsset vta);
    }

    public class VisualAssetStore : IVisualAssetStore
    {
        private readonly Dictionary<string, VisualTreeAsset> _visualTreeAssets;

        public VisualAssetStore(Dictionary<string, VisualTreeAsset> visualTreeAssets)
        {
            _visualTreeAssets = visualTreeAssets;
        }

        public VisualTreeAsset GetAsset(string key)
        {
            if (_visualTreeAssets.TryGetValue(key, out var vta))
            {
                return vta;
            }

            Debug.LogError($"VisualTreeAsset key {key} not found!");
            return null;
        }

        public bool TryGetAsset(string key, out VisualTreeAsset vta)
        {
            return _visualTreeAssets.TryGetValue(key, out vta);
        }
    }
}
