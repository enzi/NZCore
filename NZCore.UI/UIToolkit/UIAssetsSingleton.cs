using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    public class UIAssetsSingleton : IComponentData
    {
        public Dictionary<string, VisualTreeAsset> VisualTreeAssets;
        public Dictionary<string, SpriteAtlas> SpriteAtlasAssets;
        public Dictionary<string, GameObject> WorldInterfaceAssets;
    }
}