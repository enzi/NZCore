// <copyright project="NZCore.MVVM" file="IViewFactory.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using UnityEngine.UIElements;

namespace NZCore.MVVM
{
    /// <summary>
    /// Interface for creating ViewModels and Views with proper DI integration.
    /// ViewModels and Views are created separately and linked via InitializeView().
    /// </summary>
    public interface IViewFactory
    {
        // ── ViewModel creation (pure C# objects) ──────────────────────────────

        TViewModel CreateViewModel<TViewModel>()
            where TViewModel : ViewModel;

        TViewModel CreateViewModel<TViewModel>(Model model)
            where TViewModel : ViewModel;

        ViewModel CreateViewModel(Type viewModelType);

        ViewModel CreateViewModel(Type viewModelType, Model model);

        TViewModel CreateViewModel<TViewModel, TModel>()
            where TViewModel : ViewModel<TModel>
            where TModel : Model, new();

        TViewModel CreateViewModel<TViewModel, TModel>(TModel model)
            where TViewModel : ViewModel<TModel>
            where TModel : Model, new();

        // ── View initialization (links a View to an existing ViewModel) ────────

        /// <summary>
        /// Creates a View of the specified type and links it to the provided ViewModel.
        /// Sets the ViewModel as the data source and calls CreateView().
        /// </summary>
        TView InitializeView<TView>(ViewModel viewModel)
            where TView : View;

        /// <summary>
        /// Instantiates a UXML layout, finds the View element of the specified type within it,
        /// opies stylesheets, injects dependencies and links it to the provided ViewModel.
        /// The View class must be declared as a custom element in the UXML.
        /// </summary>
        TView CreateViewFromUxml<TView>(string uxmlKey, ViewModel viewModel)
            where TView : View;

        /// <summary>
        /// Instantiates a UXML layout, finds the View element of the specified type within it,
        /// copies stylesheets and injects dependencies. Does not wire up a ViewModel.
        /// The View class must be declared as a custom element in the UXML.
        /// </summary>
        TView CreateViewFromUxml<TView>(string uxmlKey)
            where TView : VisualElement;

        // ── Convenience: paired RootView + RootViewModel creation ─────────────

        TRootView CreateRootView<TRootView, TRootViewModel>()
            where TRootView : RootView
            where TRootViewModel : RootViewModel;

        TRootView CreateRootView<TRootView, TRootViewModel, TModel>()
            where TRootView : RootView
            where TRootViewModel : RootViewModel<TModel>
            where TModel : Model, new();

        TRootView CreateRootView<TRootView, TRootViewModel, TModel>(TModel model)
            where TRootView : RootView
            where TRootViewModel : RootViewModel<TModel>
            where TModel : Model, new();

        // ── Convenience: paired ChildView + ChildViewModel creation ───────────

        TChildView CreateChildView<TChildView, TChildViewModel>(RootView rootView)
            where TChildView : ChildView
            where TChildViewModel : ChildViewModel;

        TChildView CreateChildView<TChildView, TChildViewModel>(RootView rootView, Model model)
            where TChildView : ChildView
            where TChildViewModel : ChildViewModel;

        TChildView CreateChildView<TChildView, TChildViewModel, TModel>(RootView rootView)
            where TChildView : ChildView
            where TChildViewModel : ChildViewModel<TModel>
            where TModel : Model, new();

        TChildView CreateChildView<TChildView, TChildViewModel, TModel>(RootView rootView, TModel model)
            where TChildView : ChildView
            where TChildViewModel : ChildViewModel<TModel>
            where TModel : Model, new();

        // ── Detached ChildView (no UIElements hierarchy addition) ─────────────

        /// <summary>
        /// Creates and initializes a ChildView+ViewModel pair without adding it to the RootView's
        /// UIElements hierarchy. Useful for views that are manually parented into sub-containers.
        /// </summary>
        TChildView CreateDetachedChildView<TChildView, TChildViewModel>(RootView rootView, Model model)
            where TChildView : ChildView
            where TChildViewModel : ChildViewModel;
    }
}