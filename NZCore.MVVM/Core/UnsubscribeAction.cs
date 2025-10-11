// <copyright project="NZCore.MVVM" file="UnsubscribeAction.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.MVVM
{
    /// <summary>
    /// A utility class that implements IDisposable to handle unsubscription from events.
    /// </summary>
    public class UnsubscribeAction : IDisposable
    {
        private readonly Action _unsubscribeAction;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the UnsubscribeAction class.
        /// </summary>
        /// <param name="unsubscribeAction">The action to execute when disposing (typically unsubscribing from an event).</param>
        public UnsubscribeAction(Action unsubscribeAction)
        {
            _unsubscribeAction = unsubscribeAction ?? throw new ArgumentNullException(nameof(unsubscribeAction));
        }

        /// <summary>
        /// Executes the unsubscribe action and marks this instance as disposed.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _unsubscribeAction?.Invoke();
            _disposed = true;
        }
    }
}