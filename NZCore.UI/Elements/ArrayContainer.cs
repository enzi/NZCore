// <copyright project="NZCore.UI" file="ArrayContainer.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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
        [UxmlAttribute("item-template")] [CreateProperty]
        public VisualTreeAsset ItemTemplate;

        [CreateProperty]
        public IList ItemsSource
        {
            get => itemsSource;
            set
            {
                itemsSource = value;
                Rebuild();
            }
        }

        private IList itemsSource;

        private void Rebuild()
        {
            Clear();
            for (int i = 0; i < itemsSource.Count; i++)
            {
                ItemTemplate.CloneSingleTree(this);
                ElementAt(i).dataSource = itemsSource[i];
            }
        }
    }
}
#endif