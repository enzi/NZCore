// <copyright project="NZCore.UI" file="UIHelperV2.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000
using System;
using System.Runtime.InteropServices;
using BovineLabs.Core.UI;
using NZCore.MVVM;
using NZCore.UIToolkit.Data;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.UIElements;
using IServiceProvider = NZCore.Inject.IServiceProvider;

namespace NZCore.UIToolkit
{
    /// <summary>
    /// Like UIHelper, but additionally creates and wires up a View (NZCore.MVVM) for the panel.
    /// The binding (T) is created as a transient via DI instead of new T() inside UIToolkitManager.
    /// Use this when the panel has a corresponding View subclass that handles UXML construction.
    /// </summary>
    public unsafe struct UIHelperV2<TViewModel, TModel, TView>
        where TViewModel : BindableViewModel, IViewModelBindingNotify<TModel>, new()
        where TModel : unmanaged, IModelBinding
        where TView : View
    {
        private readonly FixedString128Bytes _uniqueKey;
        private readonly FixedString128Bytes _assetKey;
        private readonly int _priority;
        private readonly bool _visibleOnInstantiate;

        private TModel* _data;

        private GCHandle _viewModelHandle;
        private GCHandle _viewHandle;

        private IntPtr _viewModelPtr;
        private IntPtr _viewPtr;

        public TViewModel ViewModel => (TViewModel)GCHandle.FromIntPtr(_viewModelPtr).Target;
        public TView View => (TView)GCHandle.FromIntPtr(_viewPtr).Target;

        public UIHelperV2(string uniqueKey, string assetKey, int priority = 0, bool visibleOnInstantiate = true)
        {
            _uniqueKey = uniqueKey;
            _assetKey = assetKey;
            _priority = priority;
            _visibleOnInstantiate = visibleOnInstantiate;

            _viewModelHandle = default;
            _viewHandle = default;
            _data = null;
            _viewModelPtr = default;
            _viewPtr = default;
        }

        public ref TModel Model => ref UnsafeUtility.AsRef<TModel>(_data);

        /// <summary>
        /// Just adds the view to the container, no sorting.
        /// </summary>
        public (VisualElement ve, TViewModel viewModel) LoadInterface(string containerName = null, string elementName = null) =>
            LoadInterface(UIToolkitManager.Instance.GetRoot(containerName), elementName);

        public (VisualElement ve, TViewModel viewModel) LoadInterface(VisualElement container, string elementName = null)
        {
            var (view, viewModel) = InternalCreateView(elementName);
            UIToolkitManager.Instance.AddViewAsInterface(_uniqueKey.ToString(), view, container);

            return (view, viewModel);
        }

        /// <summary>
        /// Adds the view as a sorted panel via UIToolkitManager (_priority determines order).
        /// </summary>
        public (VisualElement ve, TViewModel viewModel) LoadPanel(string containerName = null, string elementName = null) =>
            LoadPanel(UIToolkitManager.Instance.GetRoot(containerName), elementName);

        public (VisualElement ve, TViewModel viewModel) LoadPanel(VisualElement container, string elementName = null)
        {
            var (view, viewModel) = InternalCreateView(elementName);
            UIToolkitManager.Instance.AddViewAsPanel(_uniqueKey.ToString(), view, container, _priority);

            return (view, viewModel);
        }

        /// <summary>
        /// Ensures only one view occupies the container — evicts any existing tracked view first.
        /// </summary>
        public (VisualElement ve, TViewModel viewModel) LoadExclusive(string containerName = null, string elementName = null) =>
            LoadExclusive(UIToolkitManager.Instance.GetRoot(containerName), elementName);

        public (VisualElement ve, TViewModel viewModel) LoadExclusive(VisualElement container, string elementName = null)
        {
            var (view, viewModel) = InternalCreateView(elementName);
            UIToolkitManager.Instance.SetExclusiveView(_uniqueKey.ToString(), view, container);
            return (view, viewModel);
        }

        public void Unload()
        {
            UIToolkitManager.Instance.RemovePanel(_uniqueKey.ToString());

            var (viewModel, binding) = DetachHandles();
            DisposeBinding(viewModel, binding);
        }

        private (TView view, TViewModel viewModel) InternalCreateView(string elementName)
        {
            var serviceProvider = MVVMApplicationSingleton.Instance.App.GetService<IServiceProvider>();
            var viewFactory = serviceProvider.Resolve<IViewFactory>();

            // Create and initialize ViewModel
            var viewModel = viewFactory.CreateViewModel<TViewModel>();
            viewModel.Load();

            _viewModelHandle = GCHandle.Alloc(viewModel, GCHandleType.Pinned);
            _data = (TModel*)UnsafeUtility.AddressOf(ref viewModel.Value);
            _viewModelPtr = GCHandle.ToIntPtr(_viewModelHandle);

            // Create and initialize View
            var assetKey = _assetKey.ToString();
            var view = !string.IsNullOrEmpty(assetKey)
                ? viewFactory.InitializeViewFromUxml<TView>(assetKey, viewModel)
                : viewFactory.InitializeView<TView>(viewModel);

            if (elementName != null)
            {
                view.name = elementName;
            }

            _viewHandle = GCHandle.Alloc(view, GCHandleType.Pinned);
            _viewPtr = GCHandle.ToIntPtr(_viewHandle);

            return (view, viewModel);
        }

        /// <summary>Clears and returns the current GCHandle targets without disposing them.</summary>
        private (TViewModel viewModel, IDisposable binding) DetachHandles()
        {
            TViewModel viewModel = null;
            IDisposable binding = null;

            if (_viewHandle.IsAllocated)
            {
                _viewHandle.Free();
                _viewHandle = default;
            }

            if (_viewModelHandle.IsAllocated)
            {
                viewModel = (TViewModel)_viewModelHandle.Target;
                binding = viewModel as IDisposable;
                _viewModelHandle.Free();
                _viewModelHandle = default;
                _data = null;
            }

            return (viewModel, binding);
        }

        private static void DisposeBinding(TViewModel viewModel, IDisposable disposable)
        {
            viewModel?.Unload();
            disposable?.Dispose();
        }
    }
}
#endif