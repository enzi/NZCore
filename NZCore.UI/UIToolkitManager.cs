// <copyright project="NZCore" file="UIToolkitManager.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

#if UNITY_6000
using System;
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

        private readonly Dictionary<string, VisualElement> registeredElements = new();

        //private readonly Dictionary<string, (VisualElement Element, IViewModelBinding Binding)> loadedPanels = new();
        private readonly Dictionary<string, (VisualElement Element, IViewModelBinding Binding)> loadedPanels = new();
        private readonly Dictionary<string, VisualElement> loadedInterfaces = new();

        private readonly List<OrderedElement> sortedPanels = new();

        public void Awake()
        {
            Instance = this;

            UIDocument = GetComponent<UIDocument>();
            Root = UIDocument.rootVisualElement.Q<VisualElement>("root");
            DragContainer = UIDocument.rootVisualElement.Q<VisualElement>("dragContainer");
            DragImage = DragContainer.Q<VisualElement>("dragImage");
            MainButtonsContainer = Root.Q<VisualElement>("mainButtonsContainer");
        }

        public VisualElement GetRoot(string containerName = null)
        {
            return containerName == null ? Root : Root.Q<VisualElement>(containerName);
        }

        public VisualElement AddInterface(string assetKey, bool visibleOnInstantiate = true)
        {
            return AddInterface(assetKey, Root, visibleOnInstantiate);
        }

        public VisualElement AddInterface(string assetKey, string containerName = null, bool visibleOnInstantiate = true)
        {
            return AddInterface(assetKey, GetRoot(containerName), visibleOnInstantiate);
        }

        public VisualElement AddInterface(string assetKey, VisualElement rootContainer, bool visibleOnInstantiate = true)
        {
            if (Assets.VisualTreeAssets.TryGetValue(assetKey, out var asset))
            {
                return asset.CloneSingleTree(rootContainer, visibleOnInstantiate);
            }

            Debug.LogError($"Key {assetKey} was not found in assets!");
            return null;
        }

        public (VisualElement, T) AddBindableInterface<T>(string uniqueKey, string assetKey, string elementName = null, int order = 0, bool visibleOnInstantiate = true)
            where T : class, IViewModelBinding, new()
        {
            return AddBindableInterface<T>(uniqueKey, assetKey, Root, elementName, order, visibleOnInstantiate);
        }

        public (VisualElement, T) AddBindableInterface<T>(string uniqueKey, string assetKey, string containerName = null, string elementName = null, int order = 0, bool visibleOnInstantiate = true)
            where T : class, IViewModelBinding, new()
        {
            VisualElement rootContainer = GetRoot(containerName);

            return AddBindableInterface<T>(uniqueKey, assetKey, rootContainer, elementName, order, visibleOnInstantiate);
        }

        public (VisualElement, T) AddBindableInterface<T>(string uniqueKey, string assetKey, VisualElement rootContainer, string elementName = null, int order = 0, bool visibleOnInstantiate = true)
            where T : class, IViewModelBinding, new()
        {
            if (string.IsNullOrEmpty(assetKey)) // sometimes the UISystem doesn't want to instantiate a container
            {
                return (Root, default);
            }

            if (TryLoad<T>(uniqueKey, assetKey, out var container, visibleOnInstantiate, elementName))
            {
                rootContainer.Add(container.ve);
            }

            return container;
        }

        // Panel methods
        // Panels are sorted

        public VisualElement AddPanel(string uniqueKey, VisualTreeAsset asset, string elementName = null, int order = 0, bool visibleOnInstantiate = true)
        {
            var ve = CloneAndAdd(uniqueKey, asset, visibleOnInstantiate, elementName);
            AddAsSortablePanel(ve, order);

            return ve;
        }

        public VisualElement AddPanel(string uniqueKey, VisualTreeAsset asset, VisualElement rootContainer, string elementName = null, int order = 0, bool visibleOnInstantiate = true)
        {
            var ve = CloneAndAdd(uniqueKey, asset, visibleOnInstantiate, elementName);
            AddAsSortablePanel(rootContainer, ve, order);

            return ve;
        }

        public VisualElement AddPanel(string uniqueKey, string assetKey, string elementName = null, int order = 0, bool visibleOnInstantiate = true)
        {
            return AddPanel(uniqueKey, assetKey, Root, elementName, order, visibleOnInstantiate);
        }

        public VisualElement AddPanel(string uniqueKey, string assetKey, string containerName = null, string elementName = null, int order = 0, bool visibleOnInstantiate = true)
        {
            return AddPanel(uniqueKey, assetKey, GetRoot(containerName), elementName, order, visibleOnInstantiate);
        }

        public VisualElement AddPanel(string uniqueKey, string assetKey, VisualElement rootContainer, string elementName = null, int order = 0, bool visibleOnInstantiate = true)
        {
            if (string.IsNullOrEmpty(assetKey)) // sometimes the UISystem doesn't want to instantiate a container
            {
                return Root;
            }

            if (TryLoad(uniqueKey, assetKey, out var ve, visibleOnInstantiate, elementName))
            {
                AddAsSortablePanel(rootContainer, ve, order);
            }

            return ve;
        }

        /// <summary>
        /// Adds a sortable panel ve, to the default Root with a given order
        /// </summary>
        private void AddAsSortablePanel(VisualElement ve, int order)
        {
            AddAsSortablePanel(Root, ve, order);
        }

        /// <summary>
        /// Adds a sortable panel ve, to the rootContainer with a given order
        /// </summary>
        private void AddAsSortablePanel(VisualElement rootContainer, VisualElement ve, int order)
        {
            var oe = new OrderedElement(ve, order);
            sortedPanels.Add(oe);
            sortedPanels.Sort();

            var index = sortedPanels.IndexOf(oe);
            rootContainer.Insert(index, ve);
        }

        public (VisualElement, T) AddBindablePanel<T>(string uniqueKey, string assetKey, string containerName = null, string elementName = null, int order = 0, bool visibleOnInstantiate = true)
            where T : class, IViewModelBinding, new()
        {
            return AddBindablePanel<T>(uniqueKey, assetKey, GetRoot(containerName), elementName, order, visibleOnInstantiate);
        }

        public (VisualElement, T) AddBindablePanel<T>(string uniqueKey, string assetKey, VisualElement rootContainer, string elementName = null, int order = 0, bool visibleOnInstantiate = true)
            where T : class, IViewModelBinding, new()
        {
            if (string.IsNullOrEmpty(assetKey)) // sometimes the UISystem doesn't want to instantiate a container
            {
                return (Root, default);
            }

            if (TryLoad<T>(uniqueKey, assetKey, out var container, visibleOnInstantiate, elementName))
            {
                var oe = new OrderedElement(container.ve, order);
                sortedPanels.Add(oe);
                sortedPanels.Sort();

                var index = sortedPanels.IndexOf(oe);
                rootContainer.Insert(index, container.ve);
            }

            return container;
        }

        public bool UnloadOrderedPanel()
        {
            if (sortedPanels.Count == 0)
            {
                return false;
            }

            var topPanel = sortedPanels[^1];
            // todo, destroy or hide?
            //topPanel.VisualElement.RemoveFromHierarchy();
            //topPanel.VisualElement.
            sortedPanels.RemoveAt(sortedPanels.Count - 1);

            return true;
        }

        public IViewModelBinding RemovePanel(string uniqueKey)
        {
            return TryUnload(uniqueKey, out var panel) ? panel.Binding : null;
        }

        public bool TryLoad(string uniqueKey, string assetKey, out VisualElement ve, bool visibleOnInstantiate = true, string elementName = null)
        {
            if (string.IsNullOrEmpty(assetKey)) // sometimes the UISystem doesn't want to instantiate a container
            {
                ve = Root;
                return true;
            }

            if (Assets.VisualTreeAssets.TryGetValue(assetKey, out var asset))
            {
                ve = CloneAndAdd(uniqueKey, asset, visibleOnInstantiate, elementName);
                return true;
            }
            else
            {
                Debug.LogError($"Key {assetKey} was not found in assets!");

                ve = Root;
                return false;
            }
        }

        public VisualElement CloneAndAdd(string uniqueKey, VisualTreeAsset asset, bool visibleOnInstantiate = true, string elementName = null)
        {
            var ve = asset.CloneSingleTree(visibleOnInstantiate);

            if (elementName != null)
            {
                ve.name = elementName;
            }

            loadedPanels.Add(uniqueKey, (ve, default));

            return ve;
        }

        // Binding related methods

        public bool TryLoad<T>(string uniqueKey, string assetKey, VisualElement rootContainer, out (VisualElement ve, T binding) container, bool visibleOnInstantiate = true, string elementName = null)
            where T : class, IViewModelBinding, new()
        {
            if (string.IsNullOrEmpty(assetKey)) // sometimes the UISystem doesn't want to instantiate a container
            {
                container = (Root, default);
                return true;
            }

            if (Assets.VisualTreeAssets.TryGetValue(assetKey, out var asset))
            {
                var ve = asset.CloneSingleTree(rootContainer, visibleOnInstantiate);
                var binding = new T();
                ve.dataSource = binding;

                if (elementName != null)
                    ve.name = elementName;

                loadedPanels.Add(uniqueKey, (ve, binding));

                container = (ve, binding);

                return true;
            }
            else
            {
                Debug.LogError($"Key {assetKey} was not found in assets!");

                container = (Root, default);
                return false;
            }
        }

        public bool TryLoad<T>(string uniqueKey, string assetKey, out (VisualElement ve, T binding) container, bool visibleOnInstantiate = true, string elementName = null)
            where T : class, IViewModelBinding, new()
        {
            if (string.IsNullOrEmpty(assetKey)) // sometimes the UISystem doesn't want to instantiate a container
            {
                container = (Root, default);
                return true;
            }

            if (Assets.VisualTreeAssets.TryGetValue(assetKey, out var asset))
            {
                Load(uniqueKey, asset, out container, visibleOnInstantiate, elementName);

                return true;
            }
            else
            {
                Debug.LogError($"Key {assetKey} was not found in assets!");

                container = (Root, default);
                return false;
            }
        }

        public void Load<T>(string uniqueKey, VisualTreeAsset asset, out (VisualElement ve, T binding) container, bool visibleOnInstantiate = true, string elementName = null)
            where T : class, IViewModelBinding, new()
        {
            var ve = asset.CloneSingleTree(visibleOnInstantiate);
            var binding = new T();
            ve.dataSource = binding;

            if (elementName != null)
                ve.name = elementName;

            loadedPanels.Add(uniqueKey, (ve, binding));

            container = (ve, binding);
        }

        public bool TryUnload(string uniqueKey, out (VisualElement Element, IViewModelBinding Binding) container)
        {
            if (loadedPanels.TryGetValue(uniqueKey, out container))
            {
                container.Element.RemoveFromHierarchy();
                loadedPanels.Remove(uniqueKey);
                return true;
            }

            return false;
        }

        public void RegisterElement(string key, VisualElement element)
        {
            registeredElements.Add(key, element);
        }

        public bool TryGet(string tooltip, out VisualElement element)
        {
            return registeredElements.TryGetValue(tooltip, out element);
        }

        private readonly struct OrderedElement : IComparable<OrderedElement>, IEquatable<OrderedElement>
        {
            private readonly VisualElement ve;
            private readonly int order;

            public VisualElement VisualElement => ve;

            public OrderedElement(VisualElement visualElement, int order)
            {
                ve = visualElement;
                this.order = order;
            }

            public int CompareTo(OrderedElement other)
            {
                return order.CompareTo(other.order);
            }

            public bool Equals(OrderedElement other)
            {
                return ve.Equals(other.ve);
            }

            public override int GetHashCode()
            {
                return ve.GetHashCode();
            }
        }
    }
}
#endif