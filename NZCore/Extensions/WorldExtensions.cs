﻿using System;
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