using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace NZCore
{
    [NativeContainer]
    [NativeContainerIsReadOnly]
    public unsafe struct StorageInfoExposed
    {
        [NativeDisableUnsafePtrRestriction] 
        private readonly EntityComponentStore* m_EntityComponentStore;

        internal StorageInfoExposed(EntityComponentStore* entityComponentStoreComponentStore)
        {
            m_EntityComponentStore = entityComponentStoreComponentStore;
        }

        /// <summary>
        /// Reports whether the specified <see cref="Entity"/> instance still refers to a valid entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>True if the entity exists and is valid, and returns false if
        /// the Entity instance refers to an entity that has been destroyed.</returns>
        public bool Exists(Entity entity)
        {
            return m_EntityComponentStore->Exists(entity);
        }

        /// <summary>
        /// Gets an <see cref="EntityInChunkExposed"/> for the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <exception cref="System.ArgumentException">Thrown if T is zero-size.</exception>
        public EntityInChunkExposed this[Entity entity]
        {
            get
            {
                m_EntityComponentStore->AssertEntitiesExist(&entity, 1);

                var tmp = m_EntityComponentStore->GetEntityInChunk(entity);

                return new EntityInChunkExposed()
                {
                    Chunk = tmp.Chunk,
                    IndexInChunk = tmp.IndexInChunk
                };
            }
        }
    }
    
    public unsafe struct EntityInChunkExposed : IComparable<EntityInChunk>, IEquatable<EntityInChunk>
    {
        public void* Chunk;
        public int IndexInChunk;

        public int CompareTo(EntityInChunk other)
        {
            ulong lhs = (ulong)Chunk;
            ulong rhs = (ulong)other.Chunk;
            int chunkCompare = lhs < rhs ? -1 : 1;
            int indexCompare = IndexInChunk - other.IndexInChunk;
            return (lhs != rhs) ? chunkCompare : indexCompare;
        }

        public bool Equals(EntityInChunk other)
        {
            return CompareTo(other) == 0;
        }
    }
}