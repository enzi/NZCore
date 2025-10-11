// <copyright project="NZCore.UI" file="VisualElementExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UI
{
    public static class VisualElementExtensions
    {
        /// <summary>
        /// Load a style located in Packages/[packageIdentifier]/Styles/[filename].uss
        /// </summary>
        public static void LoadStyleSheetFromPackage(this VisualElement element, string packageIdentifier, string filename)
        {
            var filePath = $"Packages/{packageIdentifier}/Styles/{filename}.uss";
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(filePath);
            if (style != null)
            {
                element.styleSheets.Add(style);
            }
            else
            {
                Debug.LogError($"{filePath} could not be found!");
            }
        }
        
        /// <summary>
        /// Load a style directly from a file path
        /// </summary>
        public static void LoadStyleSheet(this VisualElement element, string filePath)
        {
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(filePath);
            if (style != null)
            {
                element.styleSheets.Add(style);
            }
            else
            {
                Debug.LogError($"{filePath} could not be found!");
            }
        }

        public static Rect GetRect(this VisualElement element)
        {
            var l = element.layout;
            return new Rect(0, 0, l.width, l.height);
        }
    }
}