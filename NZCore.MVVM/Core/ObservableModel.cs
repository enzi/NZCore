// <copyright project="NZCore.MVVM" file="ObservableModel.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NZCore.MVVM
{
    /// <summary>
    /// Delegate for property value change notifications with old and new values.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    /// <param name="oldValue">The old value of the property.</param>
    /// <param name="newValue">The new value of the property.</param>
    public delegate void PropertyValueChangedHandler(string propertyName, object oldValue, object newValue);
    /// <summary>
    /// Base class for models that support property change notification.
    /// Extends the existing Model class with observable capabilities.
    /// </summary>
    [Serializable]
    public abstract class ObservableModel : Model, INotifyPropertyChanged, INotifyPropertyChanging, IDisposable
    {
        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        
        /// <summary>
        /// Occurs when a property value is changing.
        /// </summary>
        public event PropertyChangingEventHandler PropertyChanging;
        
        /// <summary>
        /// Occurs when a property value changes, providing old and new values.
        /// </summary>
        public event PropertyValueChangedHandler PropertyValueChanged;

        /// <summary>
        /// Initializes a new instance of the ObservableModel class.
        /// </summary>
        protected ObservableModel() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the ObservableModel class with a specific GUID.
        /// </summary>
        /// <param name="guid">The GUID to use.</param>
        protected ObservableModel(UnityEngine.Hash128 guid) : base(guid)
        {
        }

        /// <summary>
        /// Sets the value of a property and raises PropertyChanged if the value changed.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="field">The backing field.</param>
        /// <param name="value">The new value.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>True if the value was changed; otherwise, false.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            var oldValue = field;
            OnPropertyChanging(propertyName);
            field = value;
            OnPropertyChanged(propertyName);
            PropertyValueChanged?.Invoke(propertyName, oldValue, value);
            return true;
        }

        /// <summary>
        /// Sets the value of a property and raises PropertyChanged if the value changed.
        /// Returns the old value for use in property-specific change notifications.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="field">The backing field.</param>
        /// <param name="value">The new value.</param>
        /// <param name="oldValue">The previous value of the property.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>True if the value was changed; otherwise, false.</returns>
        protected bool SetProperty<T>(ref T field, T value, out T oldValue, [CallerMemberName] string propertyName = "")
        {
            oldValue = field;
            
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            OnPropertyChanging(propertyName);
            field = value;
            OnPropertyChanged(propertyName);
            PropertyValueChanged?.Invoke(propertyName, oldValue, value);
            return true;
        }

        
        protected virtual void OnPropertyChanging([CallerMemberName] string propertyName = "")
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
        }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Disposes the model and clears all event handlers.
        /// </summary>
        public virtual void Dispose()
        {
            PropertyChanged = null;
        }
    }
}