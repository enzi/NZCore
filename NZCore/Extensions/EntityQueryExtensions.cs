// <copyright project="NZCore" file="EntityQueryExtensions.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Burst.CompilerServices;
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
        
        public static DynamicBuffer<T> GetSingletonBufferNoSync<T>(this EntityQuery query, bool isReadOnly)
            where T : unmanaged, IBufferElementData
        {
            var impl = query._GetImpl();

            var typeIndex = TypeManager.GetTypeIndex<T>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (TypeManager.IsEnableable(typeIndex))
            {
                var typeName = typeIndex.ToFixedString();
                throw new InvalidOperationException($"Can't call GetSingletonBuffer<{typeName}>() with enableable component type {typeName}.");
            }
#endif

            impl->GetSingletonChunkAndEntity(typeIndex, out var indexInArchetype, out var chunk, out var entityIndexInChunk);
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(impl->_Access->EntityComponentStore->m_RecordToJournal != 0) && !isReadOnly)
            {
                impl->RecordSingletonJournalRW(chunk, typeIndex, EntitiesJournaling.RecordType.GetBufferRW);
            }
#endif

            var archetype = impl->_Access->EntityComponentStore->GetArchetype(chunk);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = &impl->_Access->DependencyManager->Safety;
            var bufferAccessor = ChunkIterationUtility.GetChunkBufferAccessor<T>(archetype, chunk, !isReadOnly, indexInArchetype,
                impl->_Access->EntityComponentStore->GlobalSystemVersion, safetyHandles->GetSafetyHandle(typeIndex, isReadOnly),
                safetyHandles->GetBufferSafetyHandle(typeIndex));
#else
            var bufferAccessor = ChunkIterationUtility.GetChunkBufferAccessor<T>(archetype, chunk, !isReadOnly, indexInArchetype,
                impl->_Access->EntityComponentStore->GlobalSystemVersion);
#endif

            return bufferAccessor[entityIndexInChunk];
        }
    }
}