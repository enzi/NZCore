// <copyright project="NZCore.MVVM" file="ViewFactory.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;
using IServiceProvider = NZCore.Inject.IServiceProvider;

namespace NZCore.MVVM
{
    /// <summary>
    /// Factory for creating ViewModels and Views with proper DI integration.
    /// ViewModels are pure C# objects; Views are VisualElements linked to ViewModels via InitializeView().
    /// </summary>
    [UsedImplicitly]
    public class ViewFactory : IViewFactory
    {
        private readonly IServiceProvider _container;
        private readonly IVisualAssetStore _visualAssetStore;

        public ViewFactory(IServiceProvider container, IVisualAssetStore visualAssetStore)
        {
            _container = container;
            _visualAssetStore = visualAssetStore;
        }

        // ── ViewModel creation ────────────────────────────────────────────────

        public TViewModel CreateViewModel<TViewModel>()
            where TViewModel : ViewModel
        {
            var viewModel = (TViewModel)CreateViewModelInstance(typeof(TViewModel));
            InitializeViewModel(viewModel);
            return viewModel;
        }

        public TViewModel CreateViewModel<TViewModel>(Model model)
            where TViewModel : ViewModel
        {
            var viewModel = (TViewModel)CreateViewModelInstance(typeof(TViewModel));
            InitializeViewModel(viewModel);
            model.Container = _container;
            viewModel.Model = model;
            return viewModel;
        }

        public ViewModel CreateViewModel(Type viewModelType)
        {
            var viewModel = CreateViewModelInstance(viewModelType);
            InitializeViewModel(viewModel);
            return viewModel;
        }

        public ViewModel CreateViewModel(Type viewModelType, Model model)
        {
            var viewModel = CreateViewModelInstance(viewModelType);
            InitializeViewModel(viewModel);
            model.Container = _container;
            viewModel.Model = model;
            return viewModel;
        }

        public TViewModel CreateViewModel<TViewModel, TModel>()
            where TViewModel : ViewModel<TModel>
            where TModel : Model, new()
        {
            var viewModel = (TViewModel)CreateViewModelInstance(typeof(TViewModel));
            InitializeViewModel(viewModel);
            return viewModel;
        }

        public TViewModel CreateViewModel<TViewModel, TModel>(TModel model)
            where TViewModel : ViewModel<TModel>
            where TModel : Model, new()
        {
            var viewModel = (TViewModel)CreateViewModelInstance(typeof(TViewModel));
            InitializeViewModel(viewModel);
            model.Container = _container;
            viewModel.Model = model;
            return viewModel;
        }

        // ── View initialization ───────────────────────────────────────────────

        public TView InitializeView<TView>(ViewModel viewModel)
            where TView : View
        {
            var view = (TView)_container.CreateInstance(typeof(TView));
            view.InitializeView(viewModel);
            return view;
        }

        /// <summary>
        /// Create and also initialize the viewModel
        /// </summary>
        public TView CreateViewFromUxml<TView>(string uxmlKey, ViewModel viewModel)
            where TView : View
        {
            var view = CreateViewFromUxml<TView>(uxmlKey);
            if (view == null)
                return null;

            view.InitializeView(viewModel);
            return view;
        }

        public TView CreateViewFromUxml<TView>(string uxmlKey)
            where TView : VisualElement
        {
            if (!_visualAssetStore.TryGetAsset(uxmlKey, out var vta))
            {
                Debug.LogError($"VisualTreeAsset '{uxmlKey}' not found!");
                return null;
            }

            var container = vta.Instantiate();
            var view = container.Q<TView>();

            if (view == null)
            {
                Debug.LogError($"UXML '{uxmlKey}' does not contain an element of type {typeof(TView).Name}!");
                return null;
            }

            for (var i = 0; i < container.styleSheets.count; i++)
            {
                view.styleSheets.Add(container.styleSheets[i]);
            }

            _container.Inject(view);
            return view;
        }

        // ── Paired RootView + RootViewModel ──────────────────────────────────

        public TRootView CreateRootView<TRootView, TRootViewModel>()
            where TRootView : RootView
            where TRootViewModel : RootViewModel
        {
            var viewModel = CreateViewModel<TRootViewModel>();
            var view = (TRootView)_container.CreateInstance(typeof(TRootView));
            view.InitializeView(viewModel);
            return view;
        }

        public TRootView CreateRootView<TRootView, TRootViewModel, TModel>()
            where TRootView : RootView
            where TRootViewModel : RootViewModel<TModel>
            where TModel : Model, new()
        {
            var viewModel = CreateViewModel<TRootViewModel>();
            var model = new TModel { Container = _container };
            viewModel.Model = model;
            var view = (TRootView)_container.CreateInstance(typeof(TRootView));
            view.InitializeView(viewModel);
            return view;
        }

        public TRootView CreateRootView<TRootView, TRootViewModel, TModel>(TModel model)
            where TRootView : RootView
            where TRootViewModel : RootViewModel<TModel>
            where TModel : Model, new()
        {
            var viewModel = CreateViewModel<TRootViewModel>();
            model.Container = _container;
            viewModel.Model = model;
            var view = (TRootView)_container.CreateInstance(typeof(TRootView));
            view.InitializeView(viewModel);
            return view;
        }

        // ── Paired ChildView + ChildViewModel ────────────────────────────────

        public TChildView CreateChildView<TChildView, TChildViewModel>(RootView rootView)
            where TChildView : ChildView
            where TChildViewModel : ChildViewModel
        {
            var viewModel = CreateViewModel<TChildViewModel>();
            var childView = (TChildView)_container.CreateInstance(typeof(TChildView));
            rootView.AddChildView(childView, viewModel);
            return childView;
        }

        public TChildView CreateChildView<TChildView, TChildViewModel>(RootView rootView, Model model)
            where TChildView : ChildView
            where TChildViewModel : ChildViewModel
        {
            var viewModel = (TChildViewModel)CreateViewModelInstance(typeof(TChildViewModel));
            var childView = (TChildView)_container.CreateInstance(typeof(TChildView));
            rootView.AddChildView(childView, viewModel); // sets AssociatedView via InitializeView
            model.Container = _container;
            viewModel.Model = model; // triggers OnViewModelModelChanged on the View ✓
            return childView;
        }

        public TChildView CreateChildView<TChildView, TChildViewModel, TModel>(RootView rootView)
            where TChildView : ChildView
            where TChildViewModel : ChildViewModel<TModel>
            where TModel : Model, new()
        {
            var viewModel = (TChildViewModel)CreateViewModelInstance(typeof(TChildViewModel));
            var childView = (TChildView)_container.CreateInstance(typeof(TChildView));
            rootView.AddChildView(childView, viewModel);
            var model = new TModel { Container = _container };
            viewModel.Model = model;
            return childView;
        }

        public TChildView CreateChildView<TChildView, TChildViewModel, TModel>(RootView rootView, TModel model)
            where TChildView : ChildView
            where TChildViewModel : ChildViewModel<TModel>
            where TModel : Model, new()
        {
            var viewModel = (TChildViewModel)CreateViewModelInstance(typeof(TChildViewModel));
            var childView = (TChildView)_container.CreateInstance(typeof(TChildView));
            rootView.AddChildView(childView, viewModel);
            model.Container = _container;
            viewModel.Model = model;
            return childView;
        }

        // ── Detached ChildView ────────────────────────────────────────────────

        public TChildView CreateDetachedChildView<TChildView, TChildViewModel>(RootView rootView, Model model)
            where TChildView : ChildView
            where TChildViewModel : ChildViewModel
        {
            var rootViewModel = (RootViewModel)rootView.ViewModel;
            var viewModel = (TChildViewModel)CreateViewModelInstance(typeof(TChildViewModel));
            viewModel.SetParentRootViewModel(rootViewModel);
            InitializeViewModel(viewModel);

            var childView = (TChildView)_container.CreateInstance(typeof(TChildView));
            childView.SetParentRootView(rootView);
            childView.InitializeView(viewModel); // sets AssociatedView, calls CreateView
            model.Container = _container;
            viewModel.Model = model; // triggers OnViewModelModelChanged on the View ✓
            return childView;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private ViewModel CreateViewModelInstance(Type viewModelType)
        {
            if (viewModelType == null)
            {
                throw new ArgumentNullException(nameof(viewModelType));
            }

            if (_container == null)
            {
                throw new ArgumentNullException(nameof(_container));
            }

            if (!typeof(ViewModel).IsAssignableFrom(viewModelType))
            {
                throw new ArgumentException($"Type {viewModelType.Name} must inherit from ViewModel", nameof(viewModelType));
            }

            return (ViewModel)_container.CreateInstance(viewModelType);
        }

        private void InitializeViewModel(ViewModel viewModel)
        {
            viewModel.Initialize(_container);
        }
    }
}