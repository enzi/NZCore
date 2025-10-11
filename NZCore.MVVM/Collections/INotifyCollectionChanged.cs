// <copyright project="NZCore.MVVM" file="INotifyCollectionChanged.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections;

namespace NZCore.MVVM
{
    /// <summary>
    /// Notifies listeners of dynamic changes, such as when items get added and removed or the whole list is refreshed.
    /// </summary>
    public interface INotifyCollectionChanged
    {
        /// <summary>
        /// Occurs when the collection changes.
        /// </summary>
        event NotifyCollectionChangedEventHandler CollectionChanged;
    }

    /// <summary>
    /// Represents the method that handles the CollectionChanged event.
    /// </summary>
    /// <param name="sender">The object that raised the event.</param>
    /// <param name="e">Information about the change.</param>
    public delegate void NotifyCollectionChangedEventHandler(object sender, NotifyCollectionChangedEventArgs e);

    /// <summary>
    /// Provides data for the CollectionChanged event.
    /// </summary>
    public class NotifyCollectionChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the action that caused the event.
        /// </summary>
        public NotifyCollectionChangedAction Action { get; }

        /// <summary>
        /// Gets the list of new items involved in the change.
        /// </summary>
        public IList NewItems { get; }

        /// <summary>
        /// Gets the list of items affected by a Replace, Remove, or Move action.
        /// </summary>
        public IList OldItems { get; }

        /// <summary>
        /// Gets the index at which the change occurred.
        /// </summary>
        public int NewStartingIndex { get; }

        /// <summary>
        /// Gets the index at which a Move, Remove, or Replace action occurred.
        /// </summary>
        public int OldStartingIndex { get; }

        /// <summary>
        /// Initializes a new instance of the NotifyCollectionChangedEventArgs class for a Reset action.
        /// </summary>
        /// <param name="action">The action that caused the event. Must be Reset.</param>
        public NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction action)
        {
            if (action != NotifyCollectionChangedAction.Reset)
                throw new ArgumentException("This constructor can only be used for Reset actions.", nameof(action));

            Action = action;
            NewStartingIndex = -1;
            OldStartingIndex = -1;
        }

        /// <summary>
        /// Initializes a new instance of the NotifyCollectionChangedEventArgs class for a single item change.
        /// </summary>
        /// <param name="action">The action that caused the event.</param>
        /// <param name="changedItem">The item that was added or removed.</param>
        /// <param name="index">The index where the change occurred.</param>
        public NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction action, object changedItem, int index)
        {
            Action = action;

            if (action == NotifyCollectionChangedAction.Add)
            {
                NewItems = new[] { changedItem };
                NewStartingIndex = index;
                OldStartingIndex = -1;
            }
            else if (action == NotifyCollectionChangedAction.Remove)
            {
                OldItems = new[] { changedItem };
                OldStartingIndex = index;
                NewStartingIndex = -1;
            }
            else
            {
                throw new ArgumentException($"This constructor does not support action '{action}'.", nameof(action));
            }
        }

        /// <summary>
        /// Initializes a new instance of the NotifyCollectionChangedEventArgs class for a Replace action.
        /// </summary>
        /// <param name="action">The action that caused the event. Must be Replace.</param>
        /// <param name="newItem">The new item.</param>
        /// <param name="oldItem">The old item.</param>
        /// <param name="index">The index where the change occurred.</param>
        public NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction action, object newItem, object oldItem, int index)
        {
            if (action != NotifyCollectionChangedAction.Replace)
                throw new ArgumentException("This constructor can only be used for Replace actions.", nameof(action));

            Action = action;
            NewItems = new[] { newItem };
            OldItems = new[] { oldItem };
            NewStartingIndex = index;
            OldStartingIndex = index;
        }

        /// <summary>
        /// Initializes a new instance of the NotifyCollectionChangedEventArgs class for a Move action.
        /// </summary>
        /// <param name="action">The action that caused the event. Must be Move.</param>
        /// <param name="changedItem">The item that was moved.</param>
        /// <param name="index">The new index of the item.</param>
        /// <param name="oldIndex">The old index of the item.</param>
        public NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction action, object changedItem, int index, int oldIndex)
        {
            if (action != NotifyCollectionChangedAction.Move)
                throw new ArgumentException("This constructor can only be used for Move actions.", nameof(action));

            Action = action;
            NewItems = new[] { changedItem };
            OldItems = new[] { changedItem };
            NewStartingIndex = index;
            OldStartingIndex = oldIndex;
        }

        /// <summary>
        /// Initializes a new instance of the NotifyCollectionChangedEventArgs class for multiple items.
        /// </summary>
        /// <param name="action">The action that caused the event.</param>
        /// <param name="changedItems">The items that were added or removed.</param>
        /// <param name="startingIndex">The index where the change occurred.</param>
        public NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction action, IList changedItems, int startingIndex)
        {
            Action = action;

            if (action == NotifyCollectionChangedAction.Add)
            {
                NewItems = changedItems;
                NewStartingIndex = startingIndex;
                OldStartingIndex = -1;
            }
            else if (action == NotifyCollectionChangedAction.Remove)
            {
                OldItems = changedItems;
                OldStartingIndex = startingIndex;
                NewStartingIndex = -1;
            }
            else
            {
                throw new ArgumentException($"This constructor does not support action '{action}'.", nameof(action));
            }
        }
    }

    /// <summary>
    /// Describes the action that caused a CollectionChanged event.
    /// </summary>
    public enum NotifyCollectionChangedAction
    {
        Add,
        Remove,
        Replace,
        Move,
        Reset
    }
}