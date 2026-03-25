// <copyright project="NZCore.MVVM" file="ListChangedCommand.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.UI
{
    /// <summary>
    /// A command specifically designed for notifying list item changes.
    /// </summary>
    public class ListElementChangedCommand : ICommand
    {
        /// <summary>
        /// Event raised when a list item at a specific index has changed.
        /// </summary>
        public event Action<int> ElementChanged;

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute.
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// Always returns true for this command.
        /// </summary>
        public bool CanExecute(object parameter) => true;

        /// <summary>
        /// Raises the CanExecuteChanged event.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Notifies that an item at the specified index has changed.
        /// </summary>
        public void Execute(object parameter)
        {
            if (parameter is int index)
            {
                ElementChanged?.Invoke(index);
            }
        }

        /// <summary>
        /// Notifies that an item at the specified index has changed.
        /// </summary>
        public void NotifyItemChanged(int index)
        {
            ElementChanged?.Invoke(index);
        }
    }
}