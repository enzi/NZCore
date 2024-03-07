#if UNITY_2023_3_0
using System.Collections.Generic;
using BovineLabs.Core.UI;
using NZCore.UIToolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    public class UIToolkitManager : MonoBehaviour
    {
        public static UIToolkitManager Instance;
        
        private readonly Dictionary<string, VisualElement> loadedInterfaceAssets = new();
        
        public UIAssetsSingleton Assets;

        public UIDocument UIDocument { get; private set; }
        public VisualElement Root { get; private set; }
        public VisualElement DragContainer { get; private set; }
        public VisualElement DragImage { get; private set; }
        public VisualElement MainButtonsContainer { get; private set; }

        private Dictionary<string, VisualElement> Lookup = new ();

        public void Awake()
        {
            Instance = this;
            
            UIDocument = GetComponent<UIDocument>();
            Root = UIDocument.rootVisualElement.Q<VisualElement>("root");
            DragContainer = UIDocument.rootVisualElement.Q<VisualElement>("dragContainer");
            DragImage = DragContainer.Q<VisualElement>("dragImage");
            MainButtonsContainer = Root.Q<VisualElement>("mainButtonsContainer");
        }

        public void AddInterface(string key, string containerName = null, bool visibleOnInstantiate = true)
        {
            if (loadedInterfaceAssets.TryGetValue(key, out var container)) 
                return;
            
            if (Assets.VisualTreeAssets.TryGetValue(key, out var asset))
            {
                asset.CloneTreeSingle(containerName == null ? Root : Root.Q<VisualElement>(containerName), visibleOnInstantiate);
            }
            else
            {
                Debug.LogError($"Key {key} was not found in assets!");
            }
        }

        public VisualElement AddInterface(string key, IBindingObject binding, string containerName = null, string elementName = null, int priority = 0, bool visibleOnInstantiate = true)
        {
            if (loadedInterfaceAssets.TryGetValue(key, out var container)) 
                return container;
            
            if (string.IsNullOrEmpty(key)) // sometimes the UISystem doesn't want to instantiate a container
            {
                return Root;
            }

            if (Assets.VisualTreeAssets.TryGetValue(key, out var asset))
            {
                container = asset.CloneTreeSingle(containerName == null ? Root : Root.Q<VisualElement>(containerName), visibleOnInstantiate);
                container.dataSource = binding;

                if (elementName != null)
                    container.name = elementName;

                return container;
                //OnLoad();
            }
            else
            {
                Debug.LogError($"Key {key} was not found in assets!");

                return null;
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
        
        public VisualElement AddInterface(string key, IBindingObject binding, VisualElement rootContainer, string elementName = null, int priority = 0, bool visibleOnInstantiate = true)
        {
            if (loadedInterfaceAssets.TryGetValue(key, out var container)) 
                return container;
            
            if (string.IsNullOrEmpty(key)) // sometimes the UISystem doesn't want to instantiate a container
            {
                return Root;
            }

            if (Assets.VisualTreeAssets.TryGetValue(key, out var asset))
            {
                container = asset.CloneTreeSingle(rootContainer, visibleOnInstantiate);
                container.dataSource = binding;

                if (elementName != null)
                    container.name = elementName;

                return container;
                //OnLoad();
            }
            else
            {
                Debug.LogError($"Key {key} was not found in assets!");

                return null;
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

        public void RemovePanel(string key)
        {
            Debug.Log("RemovePanel");
            //throw new System.NotImplementedException();
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