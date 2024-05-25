#if UNITY_6000
using System.Collections.Generic;
using BovineLabs.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    public class UIToolkitManager : MonoBehaviour
    {
        public static UIToolkitManager Instance;

        public UIAssetsSingleton Assets;
        public UIDocument UIDocument { get; private set; }
        public VisualElement Root { get; private set; }
        public VisualElement DragContainer { get; private set; }
        public VisualElement DragImage { get; private set; }
        public VisualElement MainButtonsContainer { get; private set; }

        private Dictionary<string, VisualElement> Lookup = new();
        private readonly Dictionary<string, (VisualElement Element, IBindingObject Binding)> loadedInterfaceAssets = new();

        public void Awake()
        {
            Instance = this;

            UIDocument = GetComponent<UIDocument>();
            Root = UIDocument.rootVisualElement.Q<VisualElement>("root");
            DragContainer = UIDocument.rootVisualElement.Q<VisualElement>("dragContainer");
            DragImage = DragContainer.Q<VisualElement>("dragImage");
            MainButtonsContainer = Root.Q<VisualElement>("mainButtonsContainer");
        }

        public void AddInterface(string assetKey, string containerName = null, bool visibleOnInstantiate = true)
        {
            //if (loadedInterfaceAssets.TryGetValue(key, out var container)) 
            //    return;

            if (Assets.VisualTreeAssets.TryGetValue(assetKey, out var asset))
            {
                asset.CloneTreeSingle(containerName == null ? Root : Root.Q<VisualElement>(containerName), visibleOnInstantiate);
            }
            else
            {
                Debug.LogError($"Key {assetKey} was not found in assets!");
            }
        }

        public (VisualElement, T) AddInterface<T>(string uniqueKey, string assetKey, string containerName = null, string elementName = null, int priority = 0, bool visibleOnInstantiate = true)
            where T : class, IBindingObject, new()
        {
            // if (loadedInterfaceAssets.TryGetValue(key, out var element))
            // {
            //     return loadedBindings.TryGetValue(element, out var binding) ? (element, (T)binding) : (element, null);
            // }

            if (string.IsNullOrEmpty(assetKey)) // sometimes the UISystem doesn't want to instantiate a container
            {
                return (Root, default);
            }

            if (Assets.VisualTreeAssets.TryGetValue(assetKey, out var asset))
            {
                var ve = asset.CloneTreeSingle(containerName == null ? Root : Root.Q<VisualElement>(containerName), visibleOnInstantiate);
                var binding = new T();
                ve.dataSource = binding;

                if (elementName != null)
                    ve.name = elementName;

                loadedInterfaceAssets.Add(uniqueKey, (ve, binding));

                return (ve, binding);
            }
            else
            {
                Debug.LogError($"Key {assetKey} was not found in assets!");

                return (null, default);
            }

            //element.pickingMode = PickingMode.Ignore;
            //element.AddToClassList(PanelClassName);
            // var e = new OrderedElement(element, priority);
            //
            // this.elements.Add(e);
            // this.elements.Sort();
            //
            // var index = this.elements.IndexOf(e);
            // this.view.Insert(index, element);
        }

        public (VisualElement, T) AddInterface<T>(string uniqueKey, string assetKey, VisualElement rootContainer, string elementName = null, int priority = 0, bool visibleOnInstantiate = true)
            where T : class, IBindingObject, new()
        {
            // if (loadedInterfaceAssets.TryGetValue(key, out var element))
            // {
            //     return loadedBindings.TryGetValue(element, out var binding) ? (element, (T)binding) : (element, null);
            // }

            if (string.IsNullOrEmpty(assetKey)) // sometimes the UISystem doesn't want to instantiate a container
            {
                return (Root, default);
            }

            if (Assets.VisualTreeAssets.TryGetValue(assetKey, out var asset))
            {
                var ve = asset.CloneTreeSingle(rootContainer, visibleOnInstantiate);
                var binding = new T();
                ve.dataSource = binding;

                if (elementName != null)
                    ve.name = elementName;

                loadedInterfaceAssets.Add(uniqueKey, (ve, binding));

                return (ve, binding);
            }
            else
            {
                Debug.LogError($"Key {assetKey} was not found in assets!");

                return (null, default);
            }

            //element.pickingMode = PickingMode.Ignore;
            //element.AddToClassList(PanelClassName);
            // var e = new OrderedElement(element, priority);
            //
            // this.elements.Add(e);
            // this.elements.Sort();
            //
            // var index = this.elements.IndexOf(e);
            // this.view.Insert(index, element);
        }

        public IBindingObject RemovePanel(string uniqueKey)
        {
            return TryUnloadPanel(uniqueKey, out var panel) ? panel.Binding : null;
        }

        public bool TryUnloadPanel(string uniqueKey, out (VisualElement Element, IBindingObject Binding) container)
        {
            if (loadedInterfaceAssets.TryGetValue(uniqueKey, out container))
            {
                container.Element.RemoveFromHierarchy();
                loadedInterfaceAssets.Remove(uniqueKey);
                return true;
            }

            return false;
        }

        public void RegisterElement(string key, VisualElement element)
        {
            Lookup.Add(key, element);
        }

        public bool TryGet(string tooltip, out VisualElement element)
        {
            return Lookup.TryGetValue(tooltip, out element);
        }
    }
}
#endif