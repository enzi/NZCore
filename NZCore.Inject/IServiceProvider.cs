// <copyright project="Assembly-CSharp" file="IDIContainer.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.Inject
{
    /// <summary>
    /// Interface for a dependency injection container.
    /// </summary>
    public interface IServiceProvider : IDisposable
    {
        /// <summary>
        /// Registers a service with the container.
        /// </summary>
        void Register(IServiceDescriptor descriptor);
        
        /// <summary>
        /// Registers a service type with a specific implementation type and lifetime.
        /// </summary>
        void Register<TService, TImplementation>(ServiceLifetime lifetime) where TImplementation : class, TService;
        
        /// <summary>
        /// Registers a service type with a factory and lifetime.
        /// </summary>
        void Register<TService>(ServiceLifetime lifetime, Func<IServiceProvider, object> factory) where TService : class;
        
        /// <summary>
        /// Registers a singleton instance.
        /// </summary>
        void RegisterSingleton<TService>(TService instance) where TService : class;
        
        /// <summary>
        /// Resolves a service from the container.
        /// </summary>
        T Resolve<T>() where T : class;
        
        /// <summary>
        /// Resolves a service from the container.
        /// </summary>
        object Resolve(Type serviceType);

        T CreateInstance<T>();
        object CreateInstance(IServiceDescriptor descriptor);
        object CreateInstance(Type implementationType);
        
        /// <summary>
        /// Creates a new scope.
        /// </summary>
        IServiceProvider CreateScope();
    }
}