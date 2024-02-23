using System.Collections;
using NZCore.UIToolkit;
using Unity.Properties;
using UnityEngine.UIElements;

namespace NZCore.UI
{
    [UxmlElement]
    public partial class ArrayContainer : VisualElement
    {
        [UxmlAttribute("item-template")]
        [CreateProperty]
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
                ItemTemplate.CloneTreeSingle(this);
                ElementAt(i).dataSource = itemsSource[i];
            }
        }
    }
}