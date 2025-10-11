// <copyright project="NZCore.MVVM" file="IViewFactory.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.MVVM
{
    /// <summary>
    /// Interface for creating ViewModels with proper DI integration.
    /// </summary>
    public interface IViewFactory
    {
        /// <summary>
        /// Creates a ViewModel of the specified type with DI injection.
        /// </summary>
        /// <typeparam name="TViewModel">The type of ViewModel to create.</typeparam>
        
        /// <returns>The created and initialized ViewModel.</returns>
        TViewModel CreateViewModel<TViewModel>() 
            where TViewModel : ViewModel;

        /// <summary>
        /// Creates a ViewModel of the specified type with DI injection and a specific model.
        /// </summary>
        /// <typeparam name="TViewModel">The type of ViewModel to create.</typeparam>
        
        /// <param name="model">The model to associate with the ViewModel.</param>
        /// <returns>The created and initialized ViewModel.</returns>
        TViewModel CreateViewModel<TViewModel>(Model model) 
            where TViewModel : ViewModel;

        /// <summary>
        /// Creates a ViewModel of the specified type with DI injection.
        /// </summary>
        /// <param name="viewModelType">The type of ViewModel to create.</param>
        
        /// <returns>The created and initialized ViewModel.</returns>
        ViewModel CreateViewModel(Type viewModelType);

        /// <summary>
        /// Creates a ViewModel of the specified type with DI injection and a specific model.
        /// </summary>
        /// <param name="viewModelType">The type of ViewModel to create.</param>
        
        /// <param name="model">The model to associate with the ViewModel.</param>
        /// <returns>The created and initialized ViewModel.</returns>
        ViewModel CreateViewModel(Type viewModelType, Model model);

        /// <summary>
        /// Creates a strongly-typed ViewModel with DI injection.
        /// </summary>
        /// <typeparam name="TViewModel">The type of ViewModel to create.</typeparam>
        /// <typeparam name="TModel">The type of model the ViewModel manages.</typeparam>
        
        /// <returns>The created and initialized strongly-typed ViewModel.</returns>
        TViewModel CreateViewModel<TViewModel, TModel>() 
            where TViewModel : ViewModel<TModel> 
            where TModel : Model, new();

        /// <summary>
        /// Creates a strongly-typed ViewModel with DI injection and a specific model.
        /// </summary>
        /// <typeparam name="TViewModel">The type of ViewModel to create.</typeparam>
        /// <typeparam name="TModel">The type of model the ViewModel manages.</typeparam>
        
        /// <param name="model">The strongly-typed model to associate with the ViewModel.</param>
        /// <returns>The created and initialized strongly-typed ViewModel.</returns>
        TViewModel CreateViewModel<TViewModel, TModel>(TModel model) 
            where TViewModel : ViewModel<TModel> 
            where TModel : Model, new();

        public TRootView CreateRootView<TRootView, TModel>()
            where TRootView : RootView<TModel>
            where TModel : Model, new();

        public TRootView CreateRootView<TRootView, TModel>(TModel model)
            where TRootView : RootView<TModel>
            where TModel : Model, new();
        
        TChildView CreateChildViewModel<TChildView>(RootView rootView, Model model)
            where TChildView : ChildView;
        
        /// <summary>
        /// Creates a strongly-typed ChildView with DI injection.
        /// </summary>
        /// <typeparam name="TChildView">The type of ChildView to create.</typeparam>
        /// <typeparam name="TModel">The type of model the ChildView manages.</typeparam>
        /// <param name="rootView">The RootView of the ChildView.</param>
        /// <returns>The created and initialized strongly-typed ChildView.</returns>
        TChildView CreateChildViewModel<TChildView, TModel>(RootView rootView) 
            where TChildView : ChildView<TModel> 
            where TModel : Model, new();

        /// <summary>
        /// Creates a strongly-typed ViewModel with DI injection and a specific model.
        /// </summary>
        /// <typeparam name="TChildView">The type of ChildView to create.</typeparam>
        /// <typeparam name="TModel">The type of model the ViewModel manages.</typeparam>
        /// <param name="model">The strongly-typed model to associate with the ViewModel.</param>
        /// <param name="rootView">The RootView of the ChildView.</param>
        /// <returns>The created and initialized strongly-typed ViewModel.</returns>
        TChildView CreateChildViewModel<TChildView, TModel>(RootView rootView, TModel model) 
            where TChildView : ChildView 
            where TModel : Model, new();
    }
}