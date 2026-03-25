// <copyright project="NZCore.UI" file="IViewTransition.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000
using System;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    /// <summary>
    /// Implement on a VisualElement (e.g. a View subclass) to receive enter/exit animation callbacks
    /// from UIToolkitManager when the element is added to or removed from the hierarchy.
    /// </summary>
    public interface IViewTransition
    {
        /// <summary>Called immediately after <paramref name="element"/> is added to the hierarchy.</summary>
        void AnimateEnter(VisualElement element);

        /// <summary>
        /// Called before <paramref name="element"/> is removed from the hierarchy.
        /// Invoke <paramref name="onComplete"/> when the animation finishes so the manager can
        /// remove the element. You MUST call <paramref name="onComplete"/> exactly once.
        /// </summary>
        void AnimateExit(VisualElement element, Action onComplete);
    }
}
#endif
