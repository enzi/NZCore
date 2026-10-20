// <copyright project="NZCore" file="WorldExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.Internal;
using Unity.Collections;
using Unity.Entities;

namespace NZCore
{
    public static class WorldExtensions
    {
        /// <summary>
        /// Returns the managed executing system type if one is executing.
        /// </summary>
        /// <param name="world"></param>
        /// <returns></returns>
        [ExcludeFromBurstCompatTesting("")]
        public static Type ExecutingSystemType(this World world) =>
            EntityInternals.GetTypeOfSystem(world.Unmanaged, EntityInternals.GetExecutingSystem(world.Unmanaged));

        [ExcludeFromBurstCompatTesting("Returns managed type")]
        public static Type GetSystemType(this WorldUnmanaged world, SystemHandle handle) => EntityInternals.GetTypeOfSystem(world, handle);

        public static bool SystemExists<T>(this WorldUnmanaged world)
        {
            var typeIndex = TypeManager.GetSystemTypeIndex<T>();
            return world.GetExistingUnmanagedSystem(typeIndex) != SystemHandle.Null;
        }
    }
}