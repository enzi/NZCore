// <copyright project="Assembly-CSharp" file="ServiceLifetime.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore.Inject
{
    /// <summary>
    /// Enum representing different service lifetimes.
    /// </summary>
    public enum ServiceLifetime
    {
        /// <summary>
        /// A new instance is created each time the service is requested.
        /// </summary>
        Transient,
        
        /// <summary>
        /// A single instance is created per scope.
        /// </summary>
        Scoped,
        
        /// <summary>
        /// A single instance is created for the entire application.
        /// </summary>
        Singleton
    }
}