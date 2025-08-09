// <copyright project="Assembly-CSharp" file="ServiceDescriptor.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.Inject
{
    /// <summary>
    /// Implementation of a service descriptor.
    /// </summary>
    public class ServiceDescriptor : IServiceDescriptor
    {
        public Type ServiceType { get; }
        public Type ImplementationType { get; }
        public ServiceLifetime Lifetime { get; }
        public Func<IDIContainer, object> Factory { get; }

        public ServiceDescriptor(Type serviceType, Type implementationType, ServiceLifetime lifetime, Func<IDIContainer, object> factory = null)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Lifetime = lifetime;
            Factory = factory;
        }
    }
}