// <copyright project="NZCore.MVVM" file="INavigationService.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NZCore.MVVM
{
    /// <summary>
    /// Defines the contract for navigation services in the MVVM framework.
    /// Manages ViewModel navigation with proper DI scope handling.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Occurs when navigation is about to happen.
        /// </summary>
        event Action<NavigationEventArgs> Navigating;

        /// <summary>
        /// Occurs after navigation has completed.
        /// </summary>
        event Action<NavigationEventArgs> Navigated;

        /// <summary>
        /// Gets the current navigation stack.
        /// </summary>
        IReadOnlyList<NavigationEntry> NavigationStack { get; }

        /// <summary>
        /// Gets the current active ViewModel.
        /// </summary>
        ViewModel CurrentViewModel { get; }

        /// <summary>
        /// Gets a value indicating whether navigation back is possible.
        /// </summary>
        bool CanGoBack { get; }

        /// <summary>
        /// Gets a value indicating whether navigation forward is possible.
        /// </summary>
        bool CanGoForward { get; }

        /// <summary>
        /// Navigates to a ViewModel of the specified type.
        /// </summary>
        /// <typeparam name="TViewModel">The type of ViewModel to navigate to.</typeparam>
        /// <param name="parameters">Optional navigation parameters.</param>
        /// <returns>The created ViewModel instance.</returns>
        TViewModel NavigateTo<TViewModel>(NavigationParameters parameters = null) 
            where TViewModel : ViewModel;

        /// <summary>
        /// Navigates to a ViewModel of the specified type.
        /// </summary>
        /// <param name="viewModelType">The type of ViewModel to navigate to.</param>
        /// <param name="parameters">Optional navigation parameters.</param>
        /// <returns>The created ViewModel instance.</returns>
        ViewModel NavigateTo(Type viewModelType, NavigationParameters parameters = null);

        /// <summary>
        /// Navigates back to the previous ViewModel.
        /// </summary>
        /// <returns>True if navigation was successful; otherwise, false.</returns>
        bool GoBack();

        /// <summary>
        /// Navigates forward to the next ViewModel.
        /// </summary>
        /// <returns>True if navigation was successful; otherwise, false.</returns>
        bool GoForward();

        /// <summary>
        /// Clears the navigation history and navigates to a new ViewModel.
        /// </summary>
        /// <typeparam name="TViewModel">The type of ViewModel to navigate to.</typeparam>
        /// <param name="parameters">Optional navigation parameters.</param>
        /// <returns>The created ViewModel instance.</returns>
        TViewModel NavigateToRoot<TViewModel>(NavigationParameters parameters = null) 
            where TViewModel : ViewModel;

        /// <summary>
        /// Clears all navigation history.
        /// </summary>
        void ClearHistory();

        /// <summary>
        /// Removes a specific ViewModel from the navigation stack.
        /// </summary>
        /// <param name="viewModel">The ViewModel to remove.</param>
        /// <returns>True if the ViewModel was removed; otherwise, false.</returns>
        bool RemoveFromStack(ViewModel viewModel);
    }

    /// <summary>
    /// Represents navigation parameters that can be passed between ViewModels.
    /// </summary>
    public class NavigationParameters : Dictionary<string, object>
    {
        /// <summary>
        /// Gets a parameter value of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the parameter value.</typeparam>
        /// <param name="key">The parameter key.</param>
        /// <returns>The parameter value, or default if not found.</returns>
        public T GetValue<T>(string key)
        {
            return TryGetValue(key, out var value) && value is T typedValue ? typedValue : default;
        }

        /// <summary>
        /// Sets a parameter value.
        /// </summary>
        /// <param name="key">The parameter key.</param>
        /// <param name="value">The parameter value.</param>
        /// <returns>This NavigationParameters instance for chaining.</returns>
        public NavigationParameters SetValue(string key, object value)
        {
            this[key] = value;
            return this;
        }
    }

    /// <summary>
    /// Represents an entry in the navigation stack.
    /// </summary>
    public class NavigationEntry
    {
        /// <summary>
        /// Gets or sets the ViewModel for this navigation entry.
        /// </summary>
        public ViewModel ViewModel { get; set; }

        /// <summary>
        /// Gets or sets the navigation parameters used to create this entry.
        /// </summary>
        public NavigationParameters Parameters { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this entry was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets the type of the ViewModel.
        /// </summary>
        public Type ViewModelType => ViewModel?.GetType();
    }

    /// <summary>
    /// Provides data for navigation events.
    /// </summary>
    public class NavigationEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the ViewModel being navigated from.
        /// </summary>
        public ViewModel From { get; set; }

        /// <summary>
        /// Gets or sets the ViewModel being navigated to.
        /// </summary>
        public ViewModel To { get; set; }

        /// <summary>
        /// Gets or sets the navigation parameters.
        /// </summary>
        public NavigationParameters Parameters { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the navigation should be canceled.
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// Gets or sets the navigation mode.
        /// </summary>
        public NavigationMode Mode { get; set; }
    }

    /// <summary>
    /// Defines the navigation modes.
    /// </summary>
    public enum NavigationMode
    {
        /// <summary>
        /// Forward navigation.
        /// </summary>
        Forward,

        /// <summary>
        /// Backward navigation.
        /// </summary>
        Back,

        /// <summary>
        /// Root navigation (clears history).
        /// </summary>
        Root,

        /// <summary>
        /// Refresh current ViewModel.
        /// </summary>
        Refresh
    }
}