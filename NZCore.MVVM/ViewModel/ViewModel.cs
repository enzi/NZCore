// <copyright project="NZCore.MVVM" file="ViewModel.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using IServiceProvider = NZCore.Inject.IServiceProvider;

namespace NZCore.MVVM
{
    /// <summary>
    /// Base class for ViewModels. Pure C# class — no VisualElement dependency.
    /// Handles model binding, property change notifications, and DI integration.
    /// </summary>
    public abstract class ViewModel : INotifyPropertyChanged, IDisposable
    {
        private Model _model;

        private bool _isInitialized;
        private bool _isDisposed;

        /// <summary>
        /// The Service Provider for this ViewModel's scope.
        /// </summary>
        protected internal IServiceProvider ServiceProvider { get; private set; }

        /// <summary>
        /// The ViewModelManager for managing ViewModels and their relationships.
        /// Available after Initialize() is called.
        /// </summary>
        protected internal IViewModelManager ViewModelManager { get; private set; }

        /// <summary>
        /// The View associated with this ViewModel. Set by View.InitializeView().
        /// Used to forward model changes to the View layer.
        /// </summary>
        public View AssociatedView { get; internal set; }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the model associated with this ViewModel.
        /// </summary>
        public Model Model
        {
            get => _model;
            set => SetModel(value);
        }

        /// <summary>
        /// Gets a value indicating whether this ViewModel has been initialized.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Initializes this ViewModel with a Service Provider.
        /// </summary>
        /// <param name="container">The Service Provider to use.</param>
        public virtual void Initialize(IServiceProvider container)
        {
            if (_isInitialized)
            {
                return;
            }

            ServiceProvider = container ?? throw new ArgumentNullException(nameof(container));

            // Inject ViewModelManager from the Service Provider
            ViewModelManager = container.Resolve<IViewModelManager>();

            OnInitialize();
            _isInitialized = true;
        }

        /// <summary>
        /// Sets the model and handles change notifications.
        /// </summary>
        /// <param name="model">The new model.</param>
        protected virtual void SetModel(Model model)
        {
            if (_model == model)
            {
                return;
            }

            model.Cleanup();
            model.ClearCache();

            // Unsubscribe from old model if it's observable
            if (_model is INotifyPropertyChanged oldObservable)
            {
                oldObservable.PropertyChanged -= OnModelPropertyChanged;
            }

            var oldModel = _model;
            OnUnregisterViewModel();
            _model = model;
            OnRegisterViewModel();

            // Subscribe to new model if it's observable
            if (_model is INotifyPropertyChanged newObservable)
            {
                newObservable.PropertyChanged += OnModelPropertyChanged;
            }

            OnModelChanged(oldModel, _model);
            AssociatedView?.OnModelChanged(oldModel, _model);
            OnPropertyChanged(nameof(Model));
        }

        /// <summary>
        /// Sets the value of a property and raises PropertyChanged if the value changed.
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Resolves a service from the Service Provider.
        /// </summary>
        protected T GetService<T>() where T : class
        {
            if (ServiceProvider == null)
            {
                throw new InvalidOperationException("ViewModel has not been initialized with a Service Provider.");
            }

            return ServiceProvider.Resolve<T>();
        }

        /// <summary>
        /// Called when the ViewModel is being initialized.
        /// </summary>
        protected virtual void OnInitialize() { }

        internal abstract void OnRegisterViewModel();
        internal abstract void OnUnregisterViewModel();

        /// <summary>
        /// Called when the model changes.
        /// </summary>
        protected virtual void OnModelChanged(Model oldModel, Model newModel) { }

        /// <summary>
        /// Called when a property on the model changes.
        /// </summary>
        protected virtual void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e) { }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Called when the associated View's RemoveView() is invoked.
        /// </summary>
        public virtual void OnRemovedView() { }

        /// <summary>
        /// Called when the associated View's DeleteView() is invoked.
        /// </summary>
        public virtual void OnDeleteView(ViewModel viewInitiator) { }

        /// <summary>
        /// Disposes the ViewModel and clears all resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            // Unsubscribe from model changes
            if (_model is INotifyPropertyChanged observable)
            {
                observable.PropertyChanged -= OnModelPropertyChanged;
            }

            // Clear events
            PropertyChanged = null;

            OnDispose();
        }

        /// <summary>
        /// Called when the ViewModel is being disposed.
        /// </summary>
        protected virtual void OnDispose() { }
    }
}