// <copyright project="NZCore.UI" file="BindableListView.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.MVVM;
using Unity.Properties;
using UnityEngine.UIElements;

#if UNITY_6000
namespace NZCore.UI
{
    [UxmlElement]
    public partial class BindableListView : ListView
    {
        private ListElementChangedCommand _onItemElementChanged;

        /// <summary>
        /// Command that notifies when a list item has changed.
        /// Bind this to your ViewModel's ListChangedCommand property.
        /// </summary>
        [CreateProperty]
        public ListElementChangedCommand onItemElementChanged
        {
            get => _onItemElementChanged;
            set
            {
                // Unsubscribe from old command
                if (_onItemElementChanged != null)
                {
                    _onItemElementChanged.ItemChanged -= OnItemElementChanged;
                }

                _onItemElementChanged = value;

                // Subscribe to new command
                if (_onItemElementChanged != null)
                {
                    _onItemElementChanged.ItemChanged += OnItemElementChanged;
                }
            }
        }

        private void OnItemElementChanged(int index)
        {
            RefreshItem(index);
        }

        public BindableListView()
        {
            bindItem = (element, index) => element.dataSource = itemsSource[index];
        }
    }
}
#endif
