// <copyright project="NZCore.UI" file="UIAssetsSingleton.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    [Serializable]
    public class UIAssetsSingleton : IComponentData
    {
        public Dictionary<string, VisualTreeAsset> VisualTreeAssets;
        public Dictionary<string, SpriteAtlas> SpriteAtlasAssets;
        public Dictionary<string, GameObject> WorldInterfaceAssets;

        public UIAssetsSingleton()
        {
            VisualTreeAssets = new Dictionary<string, VisualTreeAsset>();
            SpriteAtlasAssets = new Dictionary<string, SpriteAtlas>();
            WorldInterfaceAssets = new Dictionary<string, GameObject>();
        }
    }
}