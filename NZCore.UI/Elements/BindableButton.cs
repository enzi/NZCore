// <copyright project="NZCore.UI" file="BindableButton.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Properties;
using UnityEngine.UIElements;

#if UNITY_6000
namespace NZCore.UI
{
    [UxmlElement]
    public partial class BindableButton : VisualElement
    {
        private bool internalClicked;
        private readonly Clickable clickable;

        public BindableButton()
        {
            clickable = new Clickable(TriggerClick);
            this.AddManipulator(clickable);
            focusable = true;
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