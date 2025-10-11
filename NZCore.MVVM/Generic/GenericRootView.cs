// <copyright project="NZCore.MVVM" file="GenericRootView.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.ComponentModel;

namespace NZCore.MVVM
{
    /// <summary>
    /// Generic RootView that provides strongly-typed access to a specific model type.
    /// Inherits from the base RootView class while adding type-safe model operations.
    /// </summary>
    /// <typeparam name="T">The type of model this RootView manages.</typeparam>
    public abstract class RootView<T> : RootView
        where T : Model
    {
        /// <summary>
        /// Gets or sets the strongly-typed model associated with this RootView.
        /// This property shadows the base Model property to provide type safety.
        /// </summary>
        public new T Model
        {
            get => base.Model as T;
            set => base.Model = value;
        }

        /// <summary>
        /// Sets the model and handles change notifications with type safety.
        /// </summary>
        /// <param name="model">The new strongly-typed model.</param>
        protected virtual void SetModel(T model)
        {
            base.SetModel(model);
        }

        /// <summary>
        /// Called when the model changes, providing strongly-typed model references.
        /// </summary>
        /// <param name="oldModel">The previous strongly-typed model.</param>
        /// <param name="newModel">The new strongly-typed model.</param>
        protected virtual void OnModelChanged(T oldModel, T newModel)
        {
            base.OnModelChanged(oldModel, newModel);
        }

        /// <summary>
        /// Called when a property on the strongly-typed model changes.
        /// Override this method for type-safe model property change handling.
        /// </summary>
        /// <param name="model">The strongly-typed model that changed.</param>
        /// <param name="e">The property change event args.</param>
        protected virtual void OnModelPropertyChanged(T model, PropertyChangedEventArgs e)
        {
            base.OnModelPropertyChanged(model, e);
        }

        /// <summary>
        /// Override the base model property changed handler to provide strongly-typed callbacks.
        /// </summary>
        /// <param name="sender">The model that changed.</param>
        /// <param name="e">The property change event args.</param>
        protected sealed override void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is T typedModel)
            {
                OnModelPropertyChanged(typedModel, e);
            }
            else
            {
                base.OnModelPropertyChanged(sender, e);
            }
        }

        /// <summary>
        /// Override the base model changed handler to provide strongly-typed callbacks.
        /// </summary>
        /// <param name="oldModel">The previous model.</param>
        /// <param name="newModel">The new model.</param>
        protected sealed override void OnModelChanged(Model oldModel, Model newModel)
        {
            OnModelChanged(oldModel as T, newModel as T);
        }
    }
}