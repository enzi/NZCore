// <copyright project="GraphToolkit.Runtime" file="ModelView.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEngine.UIElements;

namespace NZCore.Graph
{
    public abstract class ViewModel : View
    {
        public Model Model { get; set; }

        public ViewModel()
        {
        }
    }
}