// <copyright project="NZCore.UI" file="UIHelper.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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
        where T : class, IViewModelBinding<TD>, new()
        where TD : unmanaged, IModelBinding
    {
        private readonly FixedString128Bytes uniqueKey;
        private readonly FixedString128Bytes assetKey;
        private readonly int priority;
        private readonly bool visibleOnInstantiate;

        private GCHandle handle;
        private TD* data;

        public UIHelper(string uniqueKey, string assetKey, int priority = 0, bool visibleOnInstantiate = true)
        {
            this.uniqueKey = uniqueKey;
            this.assetKey = assetKey;
            this.priority = priority;
            this.visibleOnInstantiate = visibleOnInstantiate;

            handle = default;
            data = default;
        }

        public ref TD Model => ref UnsafeUtility.AsRef<TD>(data);

        public VisualElement LoadInterface(string containerName = null, string elementName = null)
        {
            return LoadInterface(UIToolkitManager.Instance.GetRoot(containerName), elementName);
        }

        public VisualElement LoadInterface(VisualElement container, string elementName = null)
        {
            var (ve, binding) = UIToolkitManager.Instance.AddBindableInterface<T>(uniqueKey.ToString(), assetKey.ToString(), container, elementName, priority, visibleOnInstantiate);

            handle = GCHandle.Alloc(binding.Value, GCHandleType.Pinned);
            data = (TD*)UnsafeUtility.AddressOf(ref binding.Value);

            binding.Load();

            return ve;
        }

        public VisualElement LoadPanel(string containerName = null, string elementName = null)
        {
            return LoadPanel(UIToolkitManager.Instance.GetRoot(containerName), elementName);
        }

        public VisualElement LoadPanel(VisualElement container, string elementName = null)
        {
            var (ve, binding) = UIToolkitManager.Instance.AddBindablePanel<T>(uniqueKey.ToString(), assetKey.ToString(), container, elementName, priority, visibleOnInstantiate);

            handle = GCHandle.Alloc(binding.Value, GCHandleType.Pinned);
            data = (TD*)UnsafeUtility.AddressOf(ref binding.Value);

            binding.Load();

            return ve;
        }

        public void Unload()
        {
            var binding = UIToolkitManager.Instance.RemovePanel(uniqueKey.ToString());

            if (handle.IsAllocated)
            {
                binding.Unload();
                if (binding is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                handle.Free();
                handle = default;
                data = default;
            }
        }
    }
}
#endif