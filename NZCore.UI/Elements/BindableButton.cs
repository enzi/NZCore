// <copyright project="NZCore" file="BindableButton.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using Unity.Properties;
using UnityEngine.UIElements;

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