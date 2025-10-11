// <copyright project="NZCore.MVVM" file="ViewFactory.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using JetBrains.Annotations;
using NZCore.Inject;
using IServiceProvider = NZCore.Inject.IServiceProvider;

namespace NZCore.MVVM
{
    /// <summary>
    /// Factory for creating ViewModels with proper DI integration and initialization.
    /// </summary>
    [UsedImplicitly]
    public class ViewFactory : IViewFactory
    {
        private readonly IServiceProvider container;
        private readonly IViewModelManager viewModelManager;

        public ViewFactory(IServiceProvider container, IViewModelManager viewModelManager)
        {
            this.container = container;
            this.viewModelManager = viewModelManager;
        }
        
        /// <summary>
        /// Creates a ViewModel of the specified type with DI injection.
        /// </summary>
        /// <typeparam name="TViewModel">The type of ViewModel to create.</typeparam>
        /// <returns>The created and initialized ViewModel.</returns>
        public TViewModel CreateViewModel<TViewModel>() 
            where TViewModel : ViewModel
        {
            var viewModel = (TViewModel)CreateViewModel(typeof(TViewModel));
            InitializeViewModel(viewModel);
            return viewModel;
        }

        /// <summary>
        /// Creates a ViewModel of the specified type with DI injection and a specific model.
        /// </summary>
        /// <typeparam name="TViewModel">The type of ViewModel to create.</typeparam>
        /// <param name="model">The model to associate with the ViewModel.</param>
        /// <returns>The created and initialized ViewModel.</returns>
        public TViewModel CreateViewModel<TViewModel>(Model model) 
            where TViewModel : ViewModel
        {
            var viewModel = (TViewModel) CreateViewModel(typeof(TViewModel), model);
            InitializeViewModel(viewModel);
            return viewModel;
        }

        /// <summary>
        /// Creates a ViewModel of the specified type with DI injection and a specific model.
        /// </summary>
        /// <param name="viewModelType">The type of ViewModel to create.</param>
        /// <param name="model">The model to associate with the ViewModel.</param>
        /// <returns>The created and initialized ViewModel.</returns>
        public ViewModel CreateViewModel(Type viewModelType, Model model)
        {
            var viewModel = CreateViewModel(viewModelType);
            InitializeViewModel(viewModel);
            model.Container = container;
            
            viewModel.Model = model;
            return viewModel;
        }

        /// <summary>
        /// Creates a RootView of the specified type with DI injection.
        /// </summary>
        /// <typeparam name="TRootView">The type of RootView to create.</typeparam>
        /// <returns>The created and initialized RootView.</returns>
        public TRootView CreateRootView<TRootView>() 
            where TRootView : RootView
        {
            var viewModel = (TRootView)CreateViewModel(typeof(TRootView));
            InitializeViewModel(viewModel);
            return viewModel;
        }

        /// <summary>
        /// Creates a strongly-typed ViewModel with DI injection.
        /// </summary>
        /// <typeparam name="TViewModel">The type of ViewModel to create.</typeparam>
        /// <typeparam name="TModel">The type of model the ViewModel manages.</typeparam>
        
        /// <returns>The created and initialized strongly-typed ViewModel.</returns>
        public TViewModel CreateViewModel<TViewModel, TModel>() 
            where TViewModel : ViewModel<TModel> 
            where TModel : Model, new()
        {
            var viewModel = (TViewModel)CreateViewModel(typeof(TViewModel));
            InitializeViewModel(viewModel);
            
            return viewModel;
        }

        /// <summary>
        /// Creates a strongly-typed ViewModel with DI injection and a specific model.
        /// </summary>
        /// <typeparam name="TViewModel">The type of ViewModel to create.</typeparam>
        /// <typeparam name="TModel">The type of model the ViewModel manages.</typeparam>
        
        /// <param name="model">The strongly-typed model to associate with the ViewModel.</param>
        /// <returns>The created and initialized strongly-typed ViewModel.</returns>
        public TViewModel CreateViewModel<TViewModel, TModel>(TModel model) 
            where TViewModel : ViewModel<TModel> 
            where TModel : Model, new()
        {
            var viewModel = CreateViewModel<TViewModel, TModel>();
            InitializeViewModel(viewModel);
            
            model.Container = container;
            viewModel.Model = model;
            
            //todo register view and model?
            return viewModel;
        }

        /// <summary>
        /// Creates a strongly-typed RootView with DI injection.
        /// </summary>
        /// <typeparam name="TRootView">The type of RootView to create.</typeparam>
        /// <typeparam name="TModel">The type of model the RootView manages.</typeparam>
        /// <returns>The created and initialized strongly-typed RootView.</returns>
        public TRootView CreateRootView<TRootView, TModel>() 
            where TRootView : RootView<TModel> 
            where TModel : Model, new()
        {
            var viewModel = (TRootView) CreateViewModel(typeof(TRootView));
            InitializeViewModel(viewModel);

            var model = new TModel
            {
                Container = container
            };
            viewModel.Model = model;
            return viewModel;
        }
        
        public TRootView CreateRootView<TRootView, TModel>(TModel model) 
            where TRootView : RootView<TModel> 
            where TModel : Model, new()
        {
            var viewModel = (TRootView) CreateViewModel(typeof(TRootView));
            InitializeViewModel(viewModel);
            model.Container = container;
            viewModel.Model = model;
            return viewModel;
        }
        
        public TChildView CreateChildViewModel<TChildView>(RootView rootView, Model model)
            where TChildView : ChildView
        {
            var view = (TChildView) CreateViewModel(typeof(TChildView));
            view.SetParentRootView(rootView);
            
            InitializeViewModel(view);
            model.Container = container;
            view.Model = model;
            
            return view;
        }
        
        public TChildView CreateChildViewModel<TChildView, TModel>(RootView rootView) 
            where TChildView : ChildView<TModel>
            where TModel : Model, new()
        {
            var view = (TChildView) CreateViewModel(typeof(TChildView));
            view.SetParentRootView(rootView);
            
            InitializeViewModel(view);
            view.Model = new TModel() { Container = container };
            
            return view;
        }

        public TChildView CreateChildViewModel<TChildView, TModel>(RootView rootView, TModel model) 
            where TChildView : ChildView
            where TModel : Model, new()
        {
            var view = (TChildView) CreateViewModel(typeof(TChildView));
            view.SetParentRootView(rootView);
            
            InitializeViewModel(view);
            model.Container = container;
            view.Model = model;
            
            return view;
        }
        
        /// <summary>
        /// Creates a ViewModel of the specified type with DI injection.
        /// </summary>
        /// <param name="viewModelType">The type of ViewModel to create.</param>
        /// <returns>The created and initialized ViewModel.</returns>
        public ViewModel CreateViewModel(Type viewModelType)
        {
            if (viewModelType == null)
                throw new ArgumentNullException(nameof(viewModelType));
            if (container == null)
                throw new ArgumentNullException(nameof(container));
            if (!typeof(ViewModel).IsAssignableFrom(viewModelType))
                throw new ArgumentException($"Type {viewModelType.Name} must inherit from ViewModel", nameof(viewModelType));

            var viewModel = (ViewModel) container.CreateInstance(viewModelType);
            return viewModel;
        }
        
        /// <summary>
        /// Initializes the ViewModel with the Service Provider and creates its view.
        /// </summary>
        /// <param name="viewModel">The ViewModel to initialize.</param>
        private void InitializeViewModel(ViewModel viewModel)
        {
            // Kick off the full init which includes
            // which also includes the CreateView call
            viewModel.Initialize(container);
        }
    }
}