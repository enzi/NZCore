// <copyright project="NZCore.MVVM" file="GenericChildView.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore.MVVM
{
    /// <summary>
    /// Generic ChildView that provides strongly-typed access to its ChildViewModel.
    /// </summary>
    /// <typeparam name="TViewModel">The type of ChildViewModel this ChildView is paired with.</typeparam>
    public abstract class ChildView<TViewModel> : ChildView
        where TViewModel : ChildViewModel
    {
        /// <summary>
        /// Gets the strongly-typed ChildViewModel for this View.
        /// </summary>
        public TViewModel ChildViewModel => (TViewModel)ViewModel;
    }
}