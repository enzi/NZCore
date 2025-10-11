// <copyright project="NZCore.MVVM" file="RootView.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;

namespace NZCore.MVVM
{
    /// <summary>
    /// Base class for root-level ViewModels that manage application or major section navigation.
    /// </summary>
    public abstract class RootView : ViewModel
    {
        private readonly List<ChildView> _childViews = new();

        /// <summary>
        /// Gets the collection of child views managed by this root view.
        /// </summary>
        protected IReadOnlyList<ChildView> ChildViews => _childViews;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            
            ViewModelManager.RegisterRootView(this);
        }

        /// <summary>
        /// Adds a child view to this root view.
        /// </summary>
        /// <param name="childView">The child view to add.</param>
        protected virtual void AddChildView(ChildView childView)
        {
            if (childView == null || _childViews.Contains(childView))
                return;

            _childViews.Add(childView);
            
            // Set parent relationship
            childView.SetParentRootView(this);
            
            // Initialize the child view with the child scope
            if (!childView.IsInitialized)
            {
                childView.Initialize(ServiceProvider);
            }

            // Add to UI hierarchy
            Add(childView);
            
            OnChildViewAdded(childView);
        }

        /// <summary>
        /// Removes a child view from this root view.
        /// </summary>
        /// <param name="childView">The child view to remove.</param>
        public virtual void RemoveChildView(ChildView childView)
        {
            if (childView == null || !_childViews.Remove(childView))
                return;

            // Remove from UI hierarchy
            if (childView.parent == this)
            {
                Remove(childView);
            }

            // Dispose the child view
            childView.Dispose();
            
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
        /// <param name="childView">The child view that was added.</param>
        protected virtual void OnChildViewAdded(ChildView childView)
        {
        }

        /// <summary>
        /// Called when a child view is removed.
        /// </summary>
        /// <param name="childView">The child view that was removed.</param>
        protected virtual void OnChildViewRemoved(ChildView childView)
        {
        }

        public override void RemoveView()
        {
            OnUnregisterViewModel();
        }

        public override void DeleteView(ViewModel viewInitiator)
        {
            OnUnregisterViewModel();
        }

        protected override void OnRegisterViewModel()
        {
            ViewModelManager.RegisterRootView(this);
        }

        protected override void OnUnregisterViewModel()
        {
            ViewModelManager.UnregisterRootView(this);
        }

        /// <summary>
        /// Disposes the root view and all child views.
        /// </summary>
        protected override void OnDispose()
        {
            ClearChildViews();
            base.OnDispose();
        }
    }
}