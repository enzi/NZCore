using System;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    public unsafe struct EntityLocationInChunk : IEquatable<EntityLocationInChunk>, IComparable<EntityLocationInChunk>
    {
        public ArchetypeChunk chunk;
        public int            indexInChunk;

        public ulong ChunkAddressAsUlong => (ulong)chunk.m_Chunk;

        public int CompareTo(EntityLocationInChunk other)
        {
            ulong lhs          = (ulong)chunk.m_Chunk;
            ulong rhs          = (ulong)other.chunk.m_Chunk;
            int   chunkCompare = lhs < rhs ? -1 : 1;
            int   indexCompare = indexInChunk - other.indexInChunk;
            return (lhs != rhs) ? chunkCompare : indexCompare;
        }

        public bool Equals(EntityLocationInChunk other)
        {
            return chunk.Equals(other.chunk) && indexInChunk.Equals(other.indexInChunk);
        }
    }

    public static unsafe class EntityManagerExposed
    {
        [GenerateTestsForBurstCompatibility]
        public static EntityLocationInChunk GetEntityLocationInChunk(this EntityManager entityManager, Entity entity)
        {
            var ecs           = entityManager.GetCheckedEntityDataAccess()->EntityComponentStore;
            var entityInChunk = ecs->GetEntityInChunk(entity);
            return new EntityLocationInChunk
            {
                chunk        = new ArchetypeChunk(entityInChunk.Chunk, ecs),
                indexInChunk = entityInChunk.IndexInChunk
            };
        }

        [GenerateTestsForBurstCompatibility]
        public static int GetIndexInChunk(this EntityManager entityManager, Entity entity)
        {
            var ecs           = entityManager.GetCheckedEntityDataAccess()->EntityComponentStore;
            var entityInChunk = ecs->GetEntityInChunk(entity);
            return entityInChunk.IndexInChunk;
        }
        
        public static StorageInfoExposed GetStorageInfoExposed(this ComponentSystemBase systemBase)
        {
            return new StorageInfoExposed(systemBase.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore);
        }

        public static void ManualIncrement(this EntityManager entityManager)
        {
            entityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
        }

        /////////////////////////////
        /// UnsafeComponentLookup ///
        /////////////////////////////
        
        public static UnsafeComponentLookup<T> GetUnsafeComponentLookup<T>(this EntityManager entityManager, bool isReadOnly = false)
            where T : unmanaged, IComponentData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            return GetUnsafeComponentLookup<T>(entityManager, typeIndex, isReadOnly);
        }

        internal static UnsafeComponentLookup<T> GetUnsafeComponentLookup<T>(this EntityManager entityManager, TypeIndex typeIndex, bool isReadOnly)
            where T : unmanaged, IComponentData
        {
            var access = entityManager.GetCheckedEntityDataAccess();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = &access->DependencyManager->Safety;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new UnsafeComponentLookup<T>(typeIndex, access, isReadOnly);
#else
            return new UnsafeComponentLookup<T>(typeIndex, access);
#endif
        }

        // public static UnsafeComponentLookup<T> GetUnsafeComponentLookup<T>(this SystemState systemState, bool isReadOnly) 
        //     where T : unmanaged, IComponentData
        // {
        //     //CheckOnUpdate_Lookup();
        //     systemState.AddReaderWriter(isReadOnly ? ComponentType.ReadOnly<T>() : ComponentType.ReadWrite<T>());
        //     return GetUnsafeComponentLookup<T>(systemState.EntityManager, isReadOnly);
        // }
        //
        // public static UnsafeComponentLookup<T> GetUnsafeComponentLookup<T>(this ComponentSystemBase systemBase,bool isReadOnly) 
        //     where T : unmanaged, IComponentData
        // {
        //     //CheckOnUpdate_Lookup();
        //     systemBase.CheckedState()->AddReaderWriter(isReadOnly ? ComponentType.ReadOnly<T>() : ComponentType.ReadWrite<T>());
        //     return GetUnsafeComponentLookup<T>(systemBase.EntityManager, isReadOnly);
        // }
        
        /////////////////////////////
        /// SharedComponentLookup ///
        /////////////////////////////
        
        public static SharedComponentLookup<T> GetSharedComponentLookup<T>(this EntityManager entityManager, bool isReadOnly)
            where T : unmanaged, ISharedComponentData
        {
            var access = entityManager.GetCheckedEntityDataAccess();
            var typeIndex = TypeManager.GetTypeIndex<T>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new SharedComponentLookup<T>(typeIndex, access, isReadOnly);
#else
            return new SharedComponentLookup<T>(typeIndex, access);
#endif
        }
        
        public static SharedComponentLookup<T> GetSharedComponentLookup<T>(ref this SystemState system, bool isReadOnly)
            where T : unmanaged, ISharedComponentData
        {
            system.AddReaderWriter(isReadOnly ? ComponentType.ReadOnly<T>() : ComponentType.ReadWrite<T>());
            return system.EntityManager.GetSharedComponentLookup<T>(isReadOnly);
        }
    }

    public static unsafe class ChunkExtension
    {
        public static BufferAccessor<T> GetRequiredBufferAccessor<T>(this ArchetypeChunk chunk, ref BufferTypeHandle<T> bufferTypeHandle)
            where T : unmanaged, IBufferElementData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(bufferTypeHandle.m_Safety0);
#endif
            var chunkPtr = chunk.m_Chunk;
            var archetype = chunkPtr->Archetype;
            var typeIndex = bufferTypeHandle.m_TypeIndex;
            if (Hint.Unlikely(bufferTypeHandle.m_LookupCache.Archetype != archetype))
            {
                bufferTypeHandle.m_LookupCache.Update(chunkPtr->Archetype, typeIndex);
            }

            byte* ptr = (bufferTypeHandle.IsReadOnly)
                ? ChunkDataUtility.GetComponentDataWithTypeRO(chunkPtr, archetype, 0, typeIndex, ref bufferTypeHandle.m_LookupCache)
                : ChunkDataUtility.GetComponentDataWithTypeRW(chunkPtr, archetype, 0, typeIndex, bufferTypeHandle.GlobalSystemVersion, ref bufferTypeHandle.m_LookupCache);

            int internalCapacity = archetype->BufferCapacities[bufferTypeHandle.m_LookupCache.IndexInArchetype];
            var length = chunk.Count;
            int stride = bufferTypeHandle.m_LookupCache.ComponentSizeOf;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new BufferAccessor<T>(ptr, length, stride, bufferTypeHandle.IsReadOnly, bufferTypeHandle.m_Safety0, bufferTypeHandle.m_Safety1, internalCapacity);
#else
            return new BufferAccessor<T>(ptr, length, stride, internalCapacity);
#endif
        }
    }
}

