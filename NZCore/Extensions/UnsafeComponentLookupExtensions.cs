// <copyright project="NZCore" file="ComponentLookupExtensions.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Burst.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    public static unsafe class UnsafeComponentLookupExtensions
    {
        
        public static EnabledRefRW<T> GetEnableRefRWNoChangeFilter<T>(ref this UnsafeComponentLookup<T> lookup, Entity entity)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(lookup.m_Safety);
#endif
            EntityComponentStore* ecs = lookup.m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, lookup.m_TypeIndex, ref lookup.m_Cache);

            int indexInBitField;
            int* ptrChunkDisabledCount;
            var ptr = ecs->GetEnabledRawRO(
                entity, lookup.m_TypeIndex, ref lookup.m_Cache, out indexInBitField, out ptrChunkDisabledCount);

            return new EnabledRefRW<T>(MakeSafeBitRef(lookup, ptr, indexInBitField), ptrChunkDisabledCount);
        }
        
        private static SafeBitRef MakeSafeBitRef<T>(in UnsafeComponentLookup<T> lookup, ulong* ptr, int offsetInBits)
            where T : unmanaged, IComponentData, IEnableableComponent
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            => new(ptr, offsetInBits, lookup.m_Safety);
#else
            => new(ptr, offsetInBits);
#endif
        
        public static void SetChangeFilter<T>(ref this UnsafeComponentLookup<T> lookup, Entity entity)
            where T : unmanaged, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(lookup.m_Safety);
#endif
            var ecs = lookup.m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, lookup.m_TypeIndex, ref lookup.m_Cache);

            var chunk = ecs->GetChunk(entity);
            var archetype = ecs->GetArchetype(chunk);

            if (Hint.Unlikely(archetype != lookup.m_Cache.Archetype))
            {
                lookup.m_Cache.Update(archetype, lookup.m_TypeIndex);
            }

            var typeIndexInArchetype = lookup.m_Cache.IndexInArchetype;
            archetype->Chunks.SetChangeVersion(typeIndexInArchetype, chunk.ListIndex, lookup.GlobalSystemVersion);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(lookup.m_Access->EntityComponentStore->m_RecordToJournal != 0))
            {
                lookup.m_Access->EntityComponentStore->GetComponentDataWithTypeRW(entity, lookup.m_TypeIndex,
                    lookup.m_GlobalSystemVersion, ref lookup.m_Cache);
            }
#endif
        }
    }
}