// <copyright project="NZCore.MVVM" file="ChildView.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore.MVVM
{
    /// <summary>
    /// Base class for child ViewModels that are managed by a parent RootView.
    /// </summary>
    public abstract class ChildView : ViewModel
    {
        private RootView rootView;

        /// <summary>
        /// Gets the parent root view that manages this child view.
        /// </summary>
        public RootView RootView => rootView;

        /// <summary>
        /// Sets the parent root view. This is typically called automatically when added to a RootView.
        /// </summary>
        /// <param name="parentRootView">The parent root view.</param>
        internal void SetParentRootView(RootView parentRootView)
        {
            if (rootView == parentRootView)
                return;

            var oldParent = rootView;
            rootView = parentRootView;
            
            OnParentRootViewChanged(oldParent, parentRootView);
        }

        /// <summary>
        /// Called when the parent root view changes.
        /// </summary>
        /// <param name="oldParent">The previous parent root view.</param>
        /// <param name="newParent">The new parent root view.</param>
        protected virtual void OnParentRootViewChanged(RootView oldParent, RootView newParent)
        {
        }

        /// <summary>
        /// Requests removal from the parent root view.
        /// </summary>
        protected virtual void RemoveFromParent()
        {
            rootView?.RemoveChildView(this);
        }

        public sealed override void RemoveView()
        {
            for (var i = Dependencies.Count - 1; i >= 0; i--)
            {
                var dependency = Dependencies[i];
                dependency.RemoveView();
            }

            Dependencies.Clear();
            
            OnRemovedView();
            
            OnUnregisterViewModel();
            rootView?.RemoveChildView(this);
            RemoveFromHierarchy();
        }
        
        public override void DeleteView(ViewModel viewInitiator)
        {
            for (var i = Dependencies.Count - 1; i >= 0; i--)
            {
                var dependency = Dependencies[i];
                dependency.DeleteView(this);
            }

            Dependencies.Clear();
            
            OnDeleteView(viewInitiator);
            
            OnUnregisterViewModel();
            rootView?.RemoveChildView(this);
            RemoveFromHierarchy();
        }

        protected override void OnRegisterViewModel()
        {
            ViewModelManager.RegisterChildView(this, rootView);
        }
        
        protected override void OnUnregisterViewModel()
        {
            ViewModelManager.UnregisterChildView(this, rootView);
        }
    }
}