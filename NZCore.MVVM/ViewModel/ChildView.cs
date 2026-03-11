// <copyright project="NZCore.MVVM" file="ChildView.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore.MVVM
{
    /// <summary>
    /// Base class for child Views that are managed by a parent RootView.
    /// Extends View (VisualElement). Pairs with a ChildViewModel.
    /// </summary>
    public abstract class ChildView : View
    {
        private RootView _rootView;

        /// <summary>
        /// Gets the parent RootView that manages this child view.
        /// </summary>
        public RootView RootView => _rootView;

        /// <summary>
        /// Sets the parent root view. Called automatically when added to a RootView.
        /// </summary>
        internal void SetParentRootView(RootView parentRootView)
        {
            _rootView = parentRootView;
        }

        /// <summary>
        /// Requests removal from the parent root view.
        /// </summary>
        protected virtual void RemoveFromParent()
        {
            _rootView?.RemoveChildView(this);
        }

        public sealed override void RemoveView()
        {
            for (var i = Dependencies.Count - 1; i >= 0; i--)
            {
                var dependency = Dependencies[i];
                dependency.RemoveView();
            }

            Dependencies.Clear();

            ViewModel?.OnRemovedView();
            OnRemovedView();

            ViewModel?.OnUnregisterViewModel();
            _rootView?.RemoveChildView(this);
            RemoveFromHierarchy();
        }

        public override void DeleteView(ViewModel viewInitiator)
        {
            for (var i = Dependencies.Count - 1; i >= 0; i--)
            {
                var dependency = Dependencies[i];
                dependency.DeleteView(viewInitiator);
            }

            Dependencies.Clear();

            ViewModel?.OnDeleteView(viewInitiator);
            OnDeleteView(viewInitiator);

            ViewModel?.OnUnregisterViewModel();
            _rootView?.RemoveChildView(this);
            RemoveFromHierarchy();
        }
    }
}