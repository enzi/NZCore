// <copyright project="NZCore.UI" file="UIHelper.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000
using System;
using System.Runtime.InteropServices;
using BovineLabs.Core.UI;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    public unsafe struct UIHelper<T, TD>
        where T : class, IViewModelBindingNotify<TD>, new()
        where TD : unmanaged, IModelBinding
    {
        private readonly FixedString128Bytes _uniqueKey;
        private readonly FixedString128Bytes _assetKey;
        private readonly int _priority;
        private readonly bool _visibleOnInstantiate;

        private GCHandle _handle;
        private TD* _data;

        public UIHelper(string uniqueKey, string assetKey, int priority = 0, bool visibleOnInstantiate = true)
        {
            _uniqueKey = uniqueKey;
            _assetKey = assetKey;
            _priority = priority;
            _visibleOnInstantiate = visibleOnInstantiate;

            _handle = default;
            _data = null;
        }

        public ref TD Model => ref UnsafeUtility.AsRef<TD>(_data);

        public VisualElement LoadInterface(string containerName = null, string elementName = null) =>
            LoadInterface(UIToolkitManager.Instance.GetRoot(containerName), elementName);

        public VisualElement LoadInterface(VisualElement container, string elementName = null)
        {
            var (ve, binding) = UIToolkitManager.Instance.AddBindableInterface<T>(_uniqueKey.ToString(), _assetKey.ToString(), container, elementName,
                _priority, _visibleOnInstantiate);

            _handle = GCHandle.Alloc(binding.Value, GCHandleType.Pinned);
            _data = (TD*)UnsafeUtility.AddressOf(ref binding.Value);

            binding.Load();

            return ve;
        }

        public (VisualElement ve, T viewModel) LoadPanel(string containerName = null, string elementName = null) =>
            LoadPanel(UIToolkitManager.Instance.GetRoot(containerName), elementName);

        public (VisualElement ve, T viewModel) LoadPanel(VisualElement container, string elementName = null)
        {
            var (ve, binding) = UIToolkitManager.Instance.AddBindablePanel<T>(_uniqueKey.ToString(), _assetKey.ToString(), container, elementName, _priority,
                _visibleOnInstantiate);

            _handle = GCHandle.Alloc(binding.Value, GCHandleType.Pinned);
            _data = (TD*)UnsafeUtility.AddressOf(ref binding.Value);

            binding.Load();

            return (ve, binding);
        }
        
        public (VisualElement ve, T viewModel) LoadPanelNew(string containerName = null, string elementName = null) =>
            LoadPanelNew(UIToolkitManager.Instance.GetRoot(containerName), elementName);

        public (VisualElement ve, T viewModel) LoadPanelNew(VisualElement container, string elementName = null)
        {
            var (ve, binding) = UIToolkitManager.Instance.AddBindablePanel<T>(_uniqueKey.ToString(), _assetKey.ToString(), container, elementName, _priority,
                _visibleOnInstantiate);
            
            _handle = GCHandle.Alloc(binding.Value, GCHandleType.Pinned);
            _data = (TD*)UnsafeUtility.AddressOf(ref binding.Value);

            binding.Load();

            return (ve, binding);
        }

        public void Unload()
        {
            var binding = (T) UIToolkitManager.Instance.RemovePanel(_uniqueKey.ToString());

            if (_handle.IsAllocated)
            {
                binding.Unload();
                if (binding is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                _handle.Free();
                _handle = default;
                _data = null;
            }
        }
    }
}
#endif