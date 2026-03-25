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
        public static Rect GetRect(this VisualElement element)
        {
            var l = element.layout;
            return new Rect(0, 0, l.width, l.height);
        }
    }
}