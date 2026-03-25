// <copyright project="NZCore.UI" file="ArrayContainer.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000
using System.Collections;
using NZCore.UIToolkit;
using Unity.Properties;
using UnityEngine.UIElements;

namespace NZCore.UI
{
    [UxmlElement]
    public partial class ArrayContainer : VisualElement
    {
        private IList _itemsSource;
        private ListElementChangedCommand _onElementChanged;
        private ListChangedCommand _onListChanged;

        [CreateProperty] [UxmlAttribute("item-template")]
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
                if (_onElementChanged != null)
                {
                    _onElementChanged.ElementChanged -= UpdateIndex;
                }

                _onElementChanged = value;

                if (_onElementChanged != null)
                {
                    _onElementChanged.ElementChanged += UpdateIndex;
                }
            }
        }

        /// <summary>
        /// Command that notifies when a list has changed.
        /// Bind this to your ViewModel's ListChangedCommand property.
        /// </summary>
        [CreateProperty]
        public ListChangedCommand onListChanged
        {
            get => _onListChanged;
            set
            {
                if (_onListChanged != null)
                {
                    _onListChanged.ListChanged -= Rebuild;
                }

                _onListChanged = value;

                if (_onListChanged != null)
                {
                    _onListChanged.ListChanged += Rebuild;
                }
            }
        }

        private void UpdateIndex(int index)
        {
            ElementAt(index).dataSource = _itemsSource[index];
        }

        private void Rebuild()
        {
            Clear();
            for (var i = 0; i < _itemsSource.Count; i++)
            {
                ItemTemplate.CloneSingleTree(this);
                ElementAt(i).dataSource = _itemsSource[i];
            }
        }
    }
}
#endif