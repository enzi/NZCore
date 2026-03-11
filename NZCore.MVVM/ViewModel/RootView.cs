// <copyright project="NZCore.MVVM" file="RootView.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;

namespace NZCore.MVVM
{
    /// <summary>
    /// Base class for root-level Views that manage child Views.
    /// Extends View (VisualElement). Pairs with a RootViewModel.
    /// </summary>
    public abstract class RootView : View
    {
        private readonly List<ChildView> _childViews = new();

        /// <summary>
        /// Gets the collection of child views managed by this root view.
        /// </summary>
        protected IReadOnlyList<ChildView> ChildViews => _childViews;

        /// <summary>
        /// Adds a child view to this root view and wires it to its ViewModel.
        /// </summary>
        public virtual void AddChildView(ChildView childView, ChildViewModel childViewModel)
        {
            if (childView == null || _childViews.Contains(childView))
            {
                return;
            }

            var rootViewModel = (RootViewModel)ViewModel;

            _childViews.Add(childView);

            // Set the ViewModel-layer parent relationship
            childViewModel.SetParentRootViewModel(rootViewModel);

            // Initialize the ChildViewModel if not already initialized
            if (!childViewModel.IsInitialized && rootViewModel != null)
            {
                childViewModel.Initialize(rootViewModel.ServiceProvider);
            }

            // Set the View-layer parent reference
            childView.SetParentRootView(this);

            // Link the View to its ViewModel (sets dataSource, calls CreateView)
            childView.InitializeView(childViewModel);

            // Add to UIElements hierarchy
            Add(childView);

            OnChildViewAdded(childView);
        }

        /// <summary>
        /// Removes a child view from this root view.
        /// </summary>
        public virtual void RemoveChildView(ChildView childView)
        {
            if (childView == null || !_childViews.Remove(childView))
            {
                return;
            }

            // Remove from UI hierarchy
            if (childView.parent == this)
            {
                Remove(childView);
            }

            // Dispose the child ViewModel
            childView.ViewModel?.Dispose();

            OnChildViewRemoved(childView);
        }

        /// <summary>
        /// Removes all child views.
        /// </summary>
        protected virtual void ClearChildViews()
        {
            var childViewsCopy = new List<ChildView>(_childViews);
            foreach (var childView in childViewsCopy)
            {
                RemoveChildView(childView);
            }
        }

        /// <summary>
        /// Called when a child view is added.
        /// </summary>
        protected virtual void OnChildViewAdded(ChildView childView) { }

        /// <summary>
        /// Called when a child view is removed.
        /// </summary>
        protected virtual void OnChildViewRemoved(ChildView childView) { }

        // ── ChildViewModel lookup helpers ─────────────────────────────────────

        protected ChildViewModel GetChildViewModel(Model model)
            => ViewModelManager?.GetChildViewModel(model, (RootViewModel)ViewModel);

        protected ChildViewModel GetChildViewModel(UnityEngine.Hash128 guid)
            => ViewModelManager?.GetChildViewModel(guid, (RootViewModel)ViewModel);

        protected TChildViewModel GetChildViewModel<TChildViewModel>(Model model)
            where TChildViewModel : ChildViewModel
            => ViewModelManager?.GetChildViewModel<TChildViewModel>(model, (RootViewModel)ViewModel);

        protected TChildViewModel GetChildViewModel<TChildViewModel>(UnityEngine.Hash128 guid)
            where TChildViewModel : ChildViewModel
            => ViewModelManager?.GetChildViewModel<TChildViewModel>(guid, (RootViewModel)ViewModel);

        public override void RemoveView()
        {
            ViewModel?.OnUnregisterViewModel();
        }

        public override void DeleteView(ViewModel viewInitiator)
        {
            ViewModel?.OnUnregisterViewModel();
        }

        /// <summary>
        /// Disposes the root view and all child views.
        /// </summary>
        public void Dispose()
        {
            ClearChildViews();
        }
    }
}