// <copyright project="NZCore.MVVM" file="ChildViewModel.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore.MVVM
{
    /// <summary>
    /// Base class for child ViewModels that are managed by a parent RootViewModel.
    /// Pure C# — no VisualElement dependency.
    /// </summary>
    public abstract class ChildViewModel : ViewModel
    {
        private RootViewModel _rootViewModel;

        /// <summary>
        /// Gets the parent RootViewModel that manages this ChildViewModel.
        /// </summary>
        public RootViewModel RootViewModel => _rootViewModel;

        /// <summary>
        /// Sets the parent RootViewModel. Called automatically when added via a RootView.
        /// </summary>
        internal void SetParentRootViewModel(RootViewModel parentRootViewModel)
        {
            if (_rootViewModel == parentRootViewModel)
            {
                return;
            }

            var oldParent = _rootViewModel;
            _rootViewModel = parentRootViewModel;
            OnParentRootViewModelChanged(oldParent, parentRootViewModel);
        }

        /// <summary>
        /// Called when the parent RootViewModel changes.
        /// </summary>
        protected virtual void OnParentRootViewModelChanged(RootViewModel oldParent, RootViewModel newParent) { }

        internal override void OnRegisterViewModel()
        {
            ViewModelManager.RegisterChildViewModel(this, _rootViewModel);
        }

        internal override void OnUnregisterViewModel()
        {
            ViewModelManager.UnregisterChildViewModel(this, _rootViewModel);
        }
    }
}