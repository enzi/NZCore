// <copyright project="NZCore.UI" file="ArrayContainer.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000
using System.Collections;
using NZCore.MVVM;
using NZCore.UIToolkit;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UI
{
    [UxmlElement]
    public partial class ArrayContainer : VisualElement
    {
        private IList _itemsSource;
        private ListElementChangedCommand _onElementChanged;
        private ListChangedCommand _onListChanged;
        
        [CreateProperty, UxmlAttribute("item-template")]
        public VisualTreeAsset ItemTemplate;

        [CreateProperty]
        public IList itemsSource
        {
            get => _itemsSource;
            set
            {
                _itemsSource = value;
                Rebuild();
            }
        }
        
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
                    _onElementChanged.ElementChanged -= UpdateIndex;
                }

                _onElementChanged = value;

                // Subscribe to new command
                if (_onElementChanged != null)
                {
                    _onElementChanged.ElementChanged += UpdateIndex;
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
                    _onListChanged.ListChanged -= Rebuild;
                }

                _onListChanged = value;

                // Subscribe to new command
                if (_onListChanged != null)
                {
                    _onListChanged.ListChanged += Rebuild;
                }
            }
        }

        private void UpdateIndex(int index)
        {
            Debug.Log($"UpdateIndex {index}");
            ElementAt(index).dataSource = _itemsSource[index];
        }

        private void Rebuild()
        {
            Clear();
            for (int i = 0; i < _itemsSource.Count; i++)
            {
                ItemTemplate.CloneSingleTree(this);
                ElementAt(i).dataSource = _itemsSource[i];
            }
        }
    }
}
#endif