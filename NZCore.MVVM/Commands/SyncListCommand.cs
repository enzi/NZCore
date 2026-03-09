// <copyright project="NZCore.MVVM" file="ListChangedCommand.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore.MVVM
{
    /// <summary>
    /// A command designed for triggering a sync in <see cref="SyncedListView"/>
    /// </summary>
    public class SyncListCommand<T> : ICommand
        where T : unmanaged
    {
        /// <summary>
        /// Event raised when a list item at a specific index has changed.
        /// </summary>
        public event Action<UnsafeList<T>> SyncList;

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
            if (parameter is UnsafeList<T> p)
            {
                SyncList?.Invoke(p);
            }
        }

        public void ExecuteTyped(UnsafeList<T> data)
        {
            SyncList?.Invoke(data);
        }
    }
}
