// <copyright project="NZCore.UI" file="BindableButton.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.MVVM;
using Unity.Properties;
using UnityEngine.UIElements;

#if UNITY_6000
namespace NZCore.UI
{
    [UxmlElement]
    public partial class BindableButton : Button
    {
        private bool _internalClicked;
        private ICommand _command;

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
                var tmp = _internalClicked;
                _internalClicked = false;
                return tmp;
            }
            set => _internalClicked = value;
        }

        [CreateProperty]
        public ICommand command
        {
            get => _command;
            set => _command = value;
        }

        public void TriggerClick()
        {
            wasClicked = true;
            NotifyPropertyChanged(nameof(wasClicked));
            _command?.Execute(null);
        }
    }
}
#endif