// <copyright project="NZCore.UI" file="BindableScrollView.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections;
using Unity.Properties;
using UnityEngine.UIElements;

namespace NZCore.UI
{
    public class BindableScrollView : ScrollView
    {
        private static readonly BindingId makeItemProperty = (BindingId)nameof(makeItem);
        private static readonly BindingId itemsSourceProperty = (BindingId)nameof(itemsSource);

        private Func<VisualElement> _makeItem;
        private IList _itemsSource;
        private ListElementChangedCommand _onElementChanged;
        private ListChangedCommand _onListChanged;

        [CreateProperty]
        public Func<VisualElement> makeItem
        {
            get => _makeItem;
            set
            {
                if (value == _makeItem)
                {
                    return;
                }

                _makeItem = value;
                NotifyPropertyChanged(in makeItemProperty);
            }
        }

        [CreateProperty]
        public IList itemsSource
        {
            get => _itemsSource;
            set
            {
                _itemsSource = value;
                Rebuild();
                NotifyPropertyChanged(in itemsSourceProperty);
            }
        }

        [CreateProperty]
        public ListElementChangedCommand onElementChanged
        {
            get => _onElementChanged;
            set
            {
                if (_onElementChanged != null)
                {
                    _onElementChanged.ElementChanged -= RefreshItem;
                }

                _onElementChanged = value;

                if (_onElementChanged != null)
                {
                    _onElementChanged.ElementChanged += RefreshItem;
                }
            }
        }

        [CreateProperty]
        public ListChangedCommand onListChanged
        {
            get => _onListChanged;
            set
            {
                if (_onListChanged != null)
                {
                    _onListChanged.ListChanged -= RefreshItems;
                }

                _onListChanged = value;

                if (_onListChanged != null)
                {
                    _onListChanged.ListChanged += RefreshItems;
                }
            }
        }

        private void Rebuild()
        {
            contentContainer.Clear();

            if (_itemsSource == null || _makeItem == null)
            {
                return;
            }

            foreach (var item in _itemsSource)
            {
                var element = _makeItem();
                element.dataSource = item;
                contentContainer.Add(element);
            }
        }

        public void RefreshItems()
        {
            if (_itemsSource == null)
            {
                return;
            }

            if (contentContainer.childCount != _itemsSource.Count)
            {
                Rebuild();
                return;
            }

            for (var i = 0; i < contentContainer.childCount; i++)
            {
                contentContainer.ElementAt(i).dataSource = _itemsSource[i];
            }
        }

        public void RefreshItem(int index)
        {
            contentContainer.ElementAt(index).dataSource = _itemsSource[index];
        }
    }
}
