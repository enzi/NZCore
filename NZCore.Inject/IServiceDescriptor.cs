// <copyright project="Assembly-CSharp" file="IServiceDescriptor.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.Inject
{
    /// <summary>
    /// Interface for a service descriptor that holds information about a service registration.
    /// </summary>
    public interface IServiceDescriptor
    {
        Type ServiceType { get; }
        Type ImplementationType { get; }
        ServiceLifetime Lifetime { get; }
        Func<IDIContainer, object> Factory { get; }
    }
}