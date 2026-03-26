// <copyright project="NZCore.UI" file="UIAssets.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    [Serializable]
    public class UIAssets
    {
        public Dictionary<string, VisualTreeAsset> VisualTreeAssets;
        public Dictionary<string, SpriteAtlas> SpriteAtlasAssets;
        public Dictionary<string, GameObject> WorldInterfaceAssets;

        public UIAssets()
        {
            VisualTreeAssets = new Dictionary<string, VisualTreeAsset>();
            SpriteAtlasAssets = new Dictionary<string, SpriteAtlas>();
            WorldInterfaceAssets = new Dictionary<string, GameObject>();
        }
    }
}