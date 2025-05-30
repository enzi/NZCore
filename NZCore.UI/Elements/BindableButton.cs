﻿// <copyright project="NZCore.UI" file="BindableButton.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Properties;
using UnityEngine.UIElements;

#if UNITY_6000
namespace NZCore.UI
{
    [UxmlElement]
    public partial class BindableButton : Button
    {
        private bool internalClicked;

        public BindableButton()
        {
            clicked += TriggerClick;
        }

        [UxmlAttribute]
        [CreateProperty]
        internal bool wasClicked
        {
            get
            {
                var tmp = internalClicked;
                internalClicked = false;
                return tmp;
            }
            set => internalClicked = value;
        }

        public void TriggerClick()
        {
            wasClicked = true;
            NotifyPropertyChanged(nameof(wasClicked));
        }
    }
}
#endif