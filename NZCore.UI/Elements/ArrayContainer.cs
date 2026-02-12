// <copyright project="NZCore.UI" file="ArrayContainer.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000
using System.Collections;
using Unity.Properties;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    [UxmlElement]
    public partial class ArrayContainer : VisualElement
    {
        private IList _itemsSource;
        
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