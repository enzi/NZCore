// <copyright project="NZCore.MVVM" file="ViewModel.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine.UIElements;
using IServiceProvider = NZCore.Inject.IServiceProvider;

namespace NZCore.MVVM
{
    /// <summary>
    /// Base class for ViewModels that combine View capabilities with Model binding and DI integration.
    /// Uses Unity's native data binding.
    /// </summary>
    public abstract class ViewModel : View, INotifyPropertyChanged, IDisposable
    {
        private Model _model;
        
        private bool _isInitialized;
        private bool _isDisposed;
        private bool _viewCreated;

        public List<ViewModel> Dependencies = new();

        /// <summary>
        /// The Service Provider for this ViewModel's scope.
        /// </summary>
        protected IServiceProvider ServiceProvider { get; private set; }

        /// <summary>
        /// The ViewModelManager for managing ViewModels and their relationships.
        /// Available after Initialize() is called.
        /// </summary>
        protected IViewModelManager ViewModelManager { get; private set; }

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
        /// Initializes a new instance of the ViewModel class.
        /// </summary>
        protected ViewModel()
        {
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        /// <summary>
        /// Initializes this ViewModel with a Service Provider.
        /// </summary>
        /// <param name="container">The Service Provider to use.</param>
        public virtual void Initialize(IServiceProvider container)
        {
            if (_isInitialized)
                return;

            ServiceProvider = container ?? throw new ArgumentNullException(nameof(container));
            
            // Inject ViewModelManager from the Service Provider
            ViewModelManager = container.Resolve<IViewModelManager>();
            
            OnInitialize();
            EnsureViewCreated();
            _isInitialized = true;
        }
        
        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            OnCustomStyleResolved(evt.customStyle);
        }

        protected virtual void OnCustomStyleResolved(ICustomStyle styles)
        {
        }

        /// <summary>
        /// Sets the model and handles change notifications.
        /// </summary>
        /// <param name="model">The new model.</param>
        protected virtual void SetModel(Model model)
        {
            if (_model == model)
                return;
            
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
            OnPropertyChanged(nameof(Model));
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

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Resolves a service from the Service Provider.
        /// </summary>
        /// <typeparam name="T">The type of service to resolve.</typeparam>
        /// <returns>The resolved service.</returns>
        protected T GetService<T>() where T : class
        {
            if (ServiceProvider == null)
                throw new InvalidOperationException("ViewModel has not been initialized with a Service Provider.");
            
            return ServiceProvider.Resolve<T>();
        }

        /// <summary>
        /// Creates the view UI. Override this method to define your UI elements.
        /// This method is automatically called during initialization if not already called.
        /// </summary>
        private void CreateViewInternal()
        {
            if (_viewCreated)
            {
                return;
            }
            
            // Default implementation calls SetupDataBinding
            SetupDataBinding();

            InstantiateLayout();
            CreateView();
            _viewCreated = true;
        }

        public virtual void InstantiateLayout() { }

        public void InstantiateLayout(string uxmlFilePath)
        {
            if (!string.IsNullOrEmpty(uxmlFilePath))
            {
                var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlFilePath);
                visualTree?.CloneTree(this);
            }
        }

        /// <summary>
        /// Ensures the view has been created. If not, creates it now.
        /// </summary>
        private void EnsureViewCreated()
        {
            if (_viewCreated)
            {
                return;
            }
            
            CreateViewInternal();
        }

        /// <summary>
        /// Sets up data binding using Unity's native binding system.
        /// Call this after creating your UI to establish data binding.
        /// </summary>
        protected virtual void SetupDataBinding()
        {
            // Set this ViewModel as the data source for Unity's native binding system
            dataSource = this;
        }

        /// <summary>
        /// Called when the ViewModel is being initialized.
        /// </summary>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>
        /// Called when the view UI should be created. Override this method to define your UI elements.
        /// This is the method developers should override instead of CreateView().
        /// </summary>
        public override void CreateView()
        {
            
        }

        protected abstract void OnRegisterViewModel();
        protected abstract void OnUnregisterViewModel();

        /// <summary>
        /// Called when the model changes.
        /// </summary>
        /// <param name="oldModel">The previous model.</param>
        /// <param name="newModel">The new model.</param>
        protected virtual void OnModelChanged(Model oldModel, Model newModel)
        {
        }

        /// <summary>
        /// Called when a property on the model changes.
        /// </summary>
        /// <param name="sender">The model that changed.</param>
        /// <param name="e">The property change event args.</param>
        protected virtual void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
        }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public abstract void RemoveView();
        public virtual void OnRemovedView() { }
        
        public abstract void DeleteView(ViewModel viewInitiator);
        public virtual void OnDeleteView(ViewModel viewInitiator) { }

        /// <summary>
        /// Disposes the ViewModel and clears all resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

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
        protected virtual void OnDispose()
        {
        }
    }
}