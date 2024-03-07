using Unity.Entities;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    public class UIRootSingleton : IComponentData
    {
        public UIDocument rootDocument;
        public VisualElement root;
        public VisualElement dragElement;
        public VisualElement dragImage;
        
        public VisualElement tooltip;

        public VisualElement mainButtonsContainer;
    }
}