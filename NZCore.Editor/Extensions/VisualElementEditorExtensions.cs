// <copyright project="NZCore.Editor" file="VisualElementEditorExtensions.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity;
using UnityEditor;
using UnityEngine.UIElements;

namespace NZCore.Editor.Extensions
{
    public static class VisualElementEditorExtensions
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
    }
}