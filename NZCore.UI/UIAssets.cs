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
        [NonSerialized] public Dictionary<string, VisualTreeAsset> VisualTreeAssets = new();
        [NonSerialized] public Dictionary<string, SpriteAtlas> SpriteAtlasAssets = new();
        [NonSerialized] public Dictionary<string, GameObject> WorldInterfaceAssets = new();
    }
}
