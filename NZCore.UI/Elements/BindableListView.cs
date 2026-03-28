// <copyright project="NZCore.UI" file="BindableListView.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Properties;
using UnityEngine.UIElements;

#if UNITY_6000
namespace NZCore.UI
{
    [UxmlElement]
    public partial class BindableListView : ListView
    {
        private ListElementChangedCommand _onElementChanged;
        private ListChangedCommand _onListChanged;

        /// <summary>
        /// Command that notifies when a list item has changed.
        /// Bind this to your ViewModel's ListChangedCommand property.
        /// </summary>
        [CreateProperty]
        public ListElementChangedCommand onElementChanged
        {
            get => _onElementChanged;
            set
            {
                // Unsubscribe from old command
                if (_onElementChanged != null)
                {
                    _onElementChanged.ElementChanged -= OnElementChanged;
                }

                _onElementChanged = value;

                // Subscribe to new command
                if (_onElementChanged != null)
                {
                    _onElementChanged.ElementChanged += OnElementChanged;
                }
            }
        }

        [CreateProperty]
        public ListChangedCommand onListChanged
        {
            get => _onListChanged;
            set
            {
                // Unsubscribe from old command
                if (_onListChanged != null)
                {
                    _onListChanged.ListChanged -= OnListChanged;
                }

                _onListChanged = value;

                // Subscribe to new command
                if (_onListChanged != null)
                {
                    _onListChanged.ListChanged += OnListChanged;
                }
            }
        }

        private void OnListChanged()
        {
            RefreshItems();
        }

        private void OnElementChanged(int index)
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