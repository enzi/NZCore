// <copyright project="GraphToolkit.Runtime" file="View.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEngine.UIElements;

namespace NZCore.MVVM
{
    public abstract class View : VisualElement
    {
        public abstract void CreateView();
    }
}