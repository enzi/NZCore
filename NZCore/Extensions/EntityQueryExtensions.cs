// <copyright project="NZCore" file="EntityQueryExtensions.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    public static unsafe class EntityQueryExtensions
    {
        public struct SharedComponentData
        {
            public int Count;
            public fixed int IndexInEntityQuery[EntityQueryFilter.SharedComponentData.Capacity];
            public fixed int SharedComponentIndex[EntityQueryFilter.SharedComponentData.Capacity];
        }

        public static bool QueryHasSharedFilter<T>(this EntityQuery query, out int scdIndex)
            where T : unmanaged, ISharedComponentData
        {
            var filters = query.GetSharedFilters();
            var requiredType = TypeManager.GetTypeIndex<T>();

            for (var i = 0; i < filters.Count; i++)
            {
                var indexInEntityQuery = filters.IndexInEntityQuery[i];
                var component = query.__impl->_QueryData->RequiredComponents[indexInEntityQuery].TypeIndex;
                if (component == requiredType)
                {
                    scdIndex = filters.SharedComponentIndex[i];
                    return true;
                }
            }

            scdIndex = -1;
            return false;
        }

        public static SharedComponentData GetSharedFilters(this EntityQuery query)
        {
            return UnsafeUtility.As<EntityQueryFilter.SharedComponentData, SharedComponentData>(ref query._GetImpl()->_Filter.Shared);
        }
    }
}