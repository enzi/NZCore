// <copyright project="Assembly-CSharp" file="DIContainer.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;

namespace NZCore.Inject
{
    /// <summary>
    /// Implementation of a dependency injection container.
    /// </summary>
    public class DIContainer : IDIContainer
    {
        private readonly Dictionary<Type, IServiceDescriptor> _serviceDescriptors = new Dictionary<Type, IServiceDescriptor>();
        private readonly Dictionary<Type, object> _singletonInstances = new Dictionary<Type, object>();
        private readonly Dictionary<Type, object> _scopedInstances = new Dictionary<Type, object>();
        private readonly IDIContainer _rootContainer;
        private bool _disposed;

        /// <summary>
        /// Creates a new root container.
        /// </summary>
        public DIContainer()
        {
            _rootContainer = this;
        }

        /// <summary>
        /// Creates a new scoped container.
        /// </summary>
        private DIContainer(IDIContainer rootContainer)
        {
            _rootContainer = rootContainer;
        }

        /// <summary>
        /// Registers a service with the container.
        /// </summary>
        public void Register(IServiceDescriptor descriptor)
        {
            _serviceDescriptors[descriptor.ServiceType] = descriptor;
        }

        /// <summary>
        /// Registers a service type with a specific implementation type and lifetime.
        /// </summary>
        public void Register<TService, TImplementation>(ServiceLifetime lifetime) where TImplementation : class, TService
        {
            Register(new ServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime));
        }

        /// <summary>
        /// Registers a service type with a factory and lifetime.
        /// </summary>
        public void Register<TService>(ServiceLifetime lifetime, Func<IDIContainer, object> factory) where TService : class
        {
            Register(new ServiceDescriptor(typeof(TService), null, lifetime, factory));
        }

        /// <summary>
        /// Registers a singleton instance.
        /// </summary>
        public void RegisterSingleton<TService>(TService instance) where TService : class
        {
            Register(new ServiceDescriptor(typeof(TService), typeof(TService), ServiceLifetime.Singleton, 
                _ => instance));
            _singletonInstances[typeof(TService)] = instance;
        }

        /// <summary>
        /// Resolves a service from the container.
        /// </summary>
        public T Resolve<T>() where T : class
        {
            return (T)Resolve(typeof(T));
        }

        /// <summary>
        /// Resolves a service from the container.
        /// </summary>
        public object Resolve(Type serviceType)
        {
            if (!_serviceDescriptors.TryGetValue(serviceType, out var descriptor))
            {
                throw new InvalidOperationException($"Service of type {serviceType.Name} is not registered.");
            }

            return GetInstance(descriptor);
        }

        /// <summary>
        /// Creates a new scope.
        /// </summary>
        public IDIContainer CreateScope()
        {
            return new DIContainer(_rootContainer);
        }

        /// <summary>
        /// Gets an instance of a service based on its descriptor.
        /// </summary>
        private object GetInstance(IServiceDescriptor descriptor)
        {
            return descriptor.Lifetime switch
            {
                ServiceLifetime.Singleton => GetSingletonInstance(descriptor),
                ServiceLifetime.Scoped => GetScopedInstance(descriptor),
                ServiceLifetime.Transient => CreateInstance(descriptor),
                _ => throw new InvalidOperationException($"Unsupported lifetime: {descriptor.Lifetime}")
            };
        }

        /// <summary>
        /// Gets or creates a singleton instance.
        /// </summary>
        private object GetSingletonInstance(IServiceDescriptor descriptor)
        {
            var rootContainer = (DIContainer)_rootContainer;
            if (rootContainer._singletonInstances.TryGetValue(descriptor.ServiceType, out var instance))
            {
                return instance;
            }

            instance = CreateInstance(descriptor);
            rootContainer._singletonInstances[descriptor.ServiceType] = instance;
            return instance;
        }

        /// <summary>
        /// Gets or creates a scoped instance.
        /// </summary>
        private object GetScopedInstance(IServiceDescriptor descriptor)
        {
            if (_scopedInstances.TryGetValue(descriptor.ServiceType, out var instance))
            {
                return instance;
            }

            instance = CreateInstance(descriptor);
            _scopedInstances[descriptor.ServiceType] = instance;
            return instance;
        }

        /// <summary>
        /// Creates a new instance of a service.
        /// </summary>
        private object CreateInstance(IServiceDescriptor descriptor)
        {
            if (descriptor.Factory != null)
            {
                return descriptor.Factory(this);
            }

            if (descriptor.ImplementationType == null)
            {
                throw new InvalidOperationException($"No implementation type or factory specified for service {descriptor.ServiceType.Name}.");
            }

            // Check if there's a parameterless constructor
            var ctor = descriptor.ImplementationType.GetConstructor(Type.EmptyTypes);
            if (ctor != null)
            {
                return Activator.CreateInstance(descriptor.ImplementationType);
            }

            // Otherwise, resolve constructor parameters
            var constructors = descriptor.ImplementationType.GetConstructors();
            if (constructors.Length == 0)
            {
                throw new InvalidOperationException($"No public constructor found for {descriptor.ImplementationType.Name}.");
            }

            // Use the constructor with the most parameters
            var constructor = constructors[0];
            foreach (var c in constructors)
            {
                if (c.GetParameters().Length > constructor.GetParameters().Length)
                {
                    constructor = c;
                }
            }

            var parameters = constructor.GetParameters();
            var parameterInstances = new object[parameters.Length];
            
            for (int i = 0; i < parameters.Length; i++)
            {
                var parameterType = parameters[i].ParameterType;
                parameterInstances[i] = Resolve(parameterType);
            }

            return Activator.CreateInstance(descriptor.ImplementationType, parameterInstances);
        }

        /// <summary>
        /// Disposes the container and all scoped instances.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Dispose scoped instances that implement IDisposable
            foreach (var instance in _scopedInstances.Values)
            {
                if (instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            // Only dispose singleton instances if this is the root container
            if (this == _rootContainer)
            {
                var rootContainer = (DIContainer)_rootContainer;
                foreach (var instance in rootContainer._singletonInstances.Values)
                {
                    if (instance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }

            _scopedInstances.Clear();
        }
    }
}