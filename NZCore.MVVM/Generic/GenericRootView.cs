// <copyright project="NZCore.MVVM" file="GenericRootView.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore.MVVM
{
    /// <summary>
    /// Generic RootView that provides strongly-typed access to its RootViewModel.
    /// </summary>
    /// <typeparam name="TViewModel">The type of RootViewModel this RootView is paired with.</typeparam>
    public abstract class RootView<TViewModel> : RootView
        where TViewModel : RootViewModel
    {
        /// <summary>
        /// Gets the strongly-typed RootViewModel for this View.
        /// </summary>
        public TViewModel RootViewModel => (TViewModel)ViewModel;
    }
}