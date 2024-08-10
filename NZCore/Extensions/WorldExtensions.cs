// <copyright project="NZCore" file="WorldExtensions.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System;
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
        public static Type ExecutingSystemType(this World world)
        {
            return world.Unmanaged.GetTypeOfSystem(world.Unmanaged.ExecutingSystem);
        }

        public static bool SystemExists<T>(this WorldUnmanaged world)
        {
            var typeIndex = TypeManager.GetSystemTypeIndex<T>();
            return world.GetExistingUnmanagedSystem(typeIndex) != SystemHandle.Null;
        }
    }
}