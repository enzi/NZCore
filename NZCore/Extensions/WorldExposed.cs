using System;
using Unity.Collections;
using Unity.Entities;

namespace NZCore
{
    public static class WorldExposedExtensions
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
    }
}

