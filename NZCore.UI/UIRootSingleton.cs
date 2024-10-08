﻿// <copyright project="NZCore.UI" file="UIRootSingleton.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    public class UIRootSingleton : IComponentData
    {
        public UIDocument rootDocument;
        public VisualElement root;
        public VisualElement dragElement;
        public VisualElement dragImage;

        public VisualElement tooltip;

        public VisualElement mainButtonsContainer;
    }
}