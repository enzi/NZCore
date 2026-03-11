// <copyright project="NZCore.MVVM" file="View.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using IServiceProvider = NZCore.Inject.IServiceProvider;

namespace NZCore.MVVM
{
    /// <summary>
    /// Base class for all Views. Extends VisualElement and holds a reference to its ViewModel.
    /// The ViewModel is set as the data source for Unity's native binding system.
    /// </summary>
    public abstract class View : VisualElement
    {
        private bool _viewCreated;

        /// <summary>
        /// The ViewModel bound to this View. Set via InitializeView().
        /// </summary>
        public ViewModel ViewModel { get; private set; }

        /// <summary>
        /// Sibling Views whose removal lifecycle is coupled to this View's lifecycle.
        /// </summary>
        public readonly List<View> Dependencies = new();

        protected View()
        {
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolvedInternal);
        }

        /// <summary>
        /// Links this View with its ViewModel, sets up data binding, and builds the UI.
        /// Called by the factory or a parent View.
        /// </summary>
        public void InitializeView(ViewModel viewModel)
        {
            if (_viewCreated)
            {
                return;
            }

            ViewModel = viewModel;
            viewModel.AssociatedView = this;
            SetupDataBinding();
            InstantiateLayout();
            CreateView();
            _viewCreated = true;

            // If the model was set before InitializeView (e.g. by factory), notify the View now
            if (viewModel.Model != null)
            {
                OnModelChanged(null, viewModel.Model);
            }
        }

        /// <summary>
        /// Sets up data binding using Unity's native binding system.
        /// </summary>
        protected virtual void SetupDataBinding()
        {
            dataSource = ViewModel;
        }

        /// <summary>
        /// Override to load a UXML layout before CreateView() is called.
        /// </summary>
        public virtual void InstantiateLayout() { }

        /// <summary>
        /// Loads and clones a UXML asset into this View.
        /// </summary>
        public void InstantiateLayout(string uxmlFilePath)
        {
            if (!string.IsNullOrEmpty(uxmlFilePath))
            {
                var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlFilePath);
                visualTree?.CloneTree(this);
            }
        }

        /// <summary>
        /// Override this method to build the UI for this View.
        /// Called once during InitializeView().
        /// </summary>
        public abstract void CreateView();

        /// <summary>
        /// Called when the ViewModel's model changes. Override in View subclasses to react to model changes
        /// without needing direct access to the ViewModel's SetModel flow.
        /// </summary>
        protected internal virtual void OnModelChanged(Model oldModel, Model newModel) { }

        /// <summary>
        /// The ServiceProvider from the associated ViewModel.
        /// </summary>
        protected IServiceProvider ServiceProvider => ViewModel?.ServiceProvider;

        /// <summary>
        /// Resolves a service from the ViewModel's ServiceProvider.
        /// </summary>
        protected T GetService<T>() where T : class => ViewModel?.ServiceProvider?.Resolve<T>();

        /// <summary>
        /// The ViewModelManager from the associated ViewModel.
        /// </summary>
        protected IViewModelManager ViewModelManager => ViewModel?.ViewModelManager;

        /// <summary>
        /// The model from the associated ViewModel.
        /// </summary>
        public Model Model => ViewModel?.Model;

        private void OnCustomStyleResolvedInternal(CustomStyleResolvedEvent evt)
        {
            OnCustomStyleResolved(evt.customStyle);
        }

        protected virtual void OnCustomStyleResolved(ICustomStyle styles) { }

        /// <summary>
        /// Removes this View and its dependencies from the UI hierarchy and unregisters from the ViewModel layer.
        /// </summary>
        public abstract void RemoveView();

        /// <summary>
        /// Called after this View has been removed.
        /// </summary>
        public virtual void OnRemovedView() { }

        /// <summary>
        /// Deletes this View and its dependencies, notifying the initiating ViewModel.
        /// </summary>
        public abstract void DeleteView(ViewModel viewInitiator);

        /// <summary>
        /// Called after this View has been deleted.
        /// </summary>
        public virtual void OnDeleteView(ViewModel viewInitiator) { }
    }
}