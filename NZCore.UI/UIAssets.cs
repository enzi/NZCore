// <copyright project="NZCore.UI" file="UIAssets.cs">
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
    public class UIAssets : IComponentData
    {
        [SerializeField] public Dictionary<string, VisualTreeAsset> VisualTreeAssets;
        [SerializeField] public Dictionary<string, SpriteAtlas> SpriteAtlasAssets;
        [SerializeField] public Dictionary<string, GameObject> WorldInterfaceAssets;

        public UIAssets()
        {
            VisualTreeAssets = new Dictionary<string, VisualTreeAsset>();
            SpriteAtlasAssets = new Dictionary<string, SpriteAtlas>();
            WorldInterfaceAssets = new Dictionary<string, GameObject>();
        }
    }
}
