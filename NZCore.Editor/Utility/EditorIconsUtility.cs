// <copyright project="NZCore" file="EditorIcons.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using UnityEditor;
using UnityEngine;

namespace NZCore.Editor
{
    public static class EditorIconsUtility
    {
        public static Texture2D LoadIconTexture(string path)
        {
            var texture = (Texture2D)AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D));
            
            // if (texture != null &&
            //     !Mathf.Approximately(texture.GetPixelsPerPoint(), (float)Bridge.GUIUtility.pixelsPerPoint) &&
            //     !Mathf.Approximately((float)Bridge.GUIUtility.pixelsPerPoint % 1f, 0.0f))
            // {
            //     texture.filterMode = FilterMode.Bilinear;
            // }

            return texture;
        }
    }
}