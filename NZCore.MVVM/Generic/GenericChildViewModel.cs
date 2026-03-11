// <copyright project="NZCore.MVVM" file="GenericChildViewModel.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.ComponentModel;

namespace NZCore.MVVM
{
    /// <summary>
    /// Generic ChildViewModel that provides strongly-typed access to a specific model type.
    /// </summary>
    /// <typeparam name="T">The type of model this ChildViewModel manages.</typeparam>
    public abstract class ChildViewModel<T> : ChildViewModel
        where T : Model
    {
        /// <summary>
        /// Gets or sets the strongly-typed model associated with this ChildViewModel.
        /// </summary>
        public new T Model
        {
            get => base.Model as T;
            set => base.Model = value;
        }

        protected virtual void SetModel(T model)
        {
            base.SetModel(model);
        }

        protected virtual void OnModelChanged(T oldModel, T newModel)
        {
            base.OnModelChanged(oldModel, newModel);
        }

        protected virtual void OnModelPropertyChanged(T model, PropertyChangedEventArgs e)
        {
            base.OnModelPropertyChanged(model, e);
        }

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

        protected sealed override void OnModelChanged(Model oldModel, Model newModel)
        {
            OnModelChanged(oldModel as T, newModel as T);
        }
    }
}