// <copyright project="NZCore.MVVM" file="MVVMApplicationServices.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.Inject;
using UnityEngine;

namespace NZCore.MVVM
{
    /// <summary>
    /// Application-level MVVM services container that provides global dependency injection
    /// for the MVVM framework. This is the composition root for all MVVM services.
    /// </summary>
    public class MVVMApplication
    {
        private IServiceProvider _container;
        
        public MVVMApplication()
        {
            _container = new ServiceProvider();
            _container.RegisterSingleton(_container); // register self
            RegisterCoreServices();
        }
        
        /// <summary>
        /// Creates a scoped container for window or component-specific services.
        /// </summary>
        /// <returns>A new scoped Service Provider.</returns>
        public IServiceProvider CreateScope()
        {
            return _container.CreateScope();
        }
        
        /// <summary>
        /// Registers core MVVM services with the application container.
        /// </summary>
        private void RegisterCoreServices()
        {
            // Core MVVM services
            _container.Register<IViewFactory, ViewFactory>(ServiceLifetime.Singleton);
            _container.Register<IViewModelManager, ViewModelManager>(ServiceLifetime.Singleton);
            
            // Navigation services (placeholder)
            
            // _container.Register<INavigationService>(ServiceLifetime.Singleton, 
            //     container => System.Activator.CreateInstance(
            //         System.Type.GetType("NZCore.MVVM.Navigation.NavigationService"), 
            //         container) as INavigationService);
        }
        
        /// <summary>
        /// Allows registration of additional application-level services.
        /// Call this during application startup after Initialize().
        /// </summary>
        /// <param name="registerAction">Action to register additional services.</param>
        public void RegisterServices(System.Action<IServiceProvider> registerAction)
        {
            if (_container == null)
            {
                Debug.LogError("MVVMApplicationServices must be initialized before registering additional services.");
                return;
            }
            
            registerAction?.Invoke(_container);
        }
        
        /// <summary>
        /// Resolves a service from the application container.
        /// </summary>
        /// <typeparam name="T">The type of service to resolve.</typeparam>
        /// <returns>The resolved service instance.</returns>
        public T GetService<T>() where T : class
        {
            return _container.Resolve<T>();
        }
        
        /// <summary>
        /// Clears the application services container. 
        /// This should only be used for testing or application shutdown.
        /// </summary>
        public void Shutdown()
        {
            if (_container is System.IDisposable disposable)
            {
                disposable.Dispose();
            }
            _container = null;
        }
    }
}