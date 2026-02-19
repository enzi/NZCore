// <copyright project="NZCore" file="UnsafeBufferLookup.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Unity.Burst.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct UnsafeBufferLookup<T> where T : unmanaged, IBufferElementData
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // the position of this is the same as BufferLookup, so they can align
        internal AtomicSafetyHandle m_Safety0;
        internal AtomicSafetyHandle m_ArrayInvalidationSafety;
        private int m_SafetyReadOnlyCount;
        private int m_SafetyReadWriteCount;

#endif
        [NativeDisableUnsafePtrRestriction] private readonly EntityDataAccess* m_Access;
        LookupCache m_Cache;
        private readonly TypeIndex m_TypeIndex;

        uint m_GlobalSystemVersion;
        int m_InternalCapacity;
        private readonly byte  m_IsReadOnly;

        internal uint GlobalSystemVersion => m_GlobalSystemVersion;


#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal UnsafeBufferLookup(TypeIndex typeIndex, EntityDataAccess* access, bool isReadOnly,
                                  AtomicSafetyHandle safety, AtomicSafetyHandle arrayInvalidationSafety)
        {
            m_Safety0 = safety;
            m_ArrayInvalidationSafety = arrayInvalidationSafety;
            m_SafetyReadOnlyCount = isReadOnly ? 2 : 0;
            m_SafetyReadWriteCount = isReadOnly ? 0 : 2;
            m_TypeIndex = typeIndex;
            m_Access = access;
            m_IsReadOnly = isReadOnly ? (byte)1 : (byte)0;
            m_Cache = default;
            m_GlobalSystemVersion = access->EntityComponentStore->GlobalSystemVersion;

            if (!TypeManager.IsBuffer(m_TypeIndex))
            {
                var typeName = m_TypeIndex.ToFixedString();
                throw new ArgumentException(
                    $"GetComponentBufferArray<{typeName}> must be IBufferElementData");
            }

            m_InternalCapacity = TypeManager.GetTypeInfo<T>().BufferCapacity;
        }

#else
        internal UnsafeBufferLookup(TypeIndex typeIndex, EntityDataAccess* access, bool isReadOnly)
        {
            m_TypeIndex = typeIndex;
            m_Access = access;
            m_IsReadOnly = isReadOnly ? (byte)1 : (byte)0;;
            m_Cache = default;
            m_GlobalSystemVersion = access->EntityComponentStore->GlobalSystemVersion;
            m_InternalCapacity = TypeManager.GetTypeInfo<T>().BufferCapacity;
        }

#endif
        public bool TryGetBuffer(Entity entity, out DynamicBuffer<T> bufferData, bool bumpVersion = true)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety0);
#endif
            var ecs = m_Access->EntityComponentStore;
            var entityExists = ecs->Exists(entity);
            if (Hint.Unlikely(!entityExists))
            {
                bufferData = default;
                return false;
            }
            
            var header = bumpVersion
                ? (BufferHeader*)ecs->GetOptionalComponentDataWithTypeRW(entity, m_TypeIndex, m_GlobalSystemVersion, ref m_Cache)
                : (BufferHeader*)ecs->GetOptionalComponentDataWithTypeRO(entity, m_TypeIndex, ref m_Cache);

            if (header != null)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                bufferData =  new DynamicBuffer<T>(header, m_Safety0, m_ArrayInvalidationSafety, m_IsReadOnly != 0, false, 0, m_InternalCapacity);
#else
                bufferData = new DynamicBuffer<T>(header, m_InternalCapacity);
#endif
                return true;
            }
            else
            {
                bufferData = default;
                return false;
            }
        }
       
        public bool EntityExists(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety0);
#endif
            var ecs = m_Access->EntityComponentStore;
            return ecs->Exists(entity);
        }
       
        public bool HasBuffer(Entity entity) => HasBuffer(entity, out _);
        
        public bool HasBuffer(Entity entity, out bool entityExists)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety0);
#endif
            var ecs = m_Access->EntityComponentStore;
            return ecs->HasComponent(entity, m_TypeIndex, ref m_Cache, out entityExists);
        }
        
        public bool DidChange(Entity entity, uint version)
        {
            var ecs = m_Access->EntityComponentStore;
            var chunk = ecs->GetChunk(entity);
            var archetype = ecs->GetArchetype(chunk);
            if (Hint.Unlikely(archetype != m_Cache.Archetype))
                m_Cache.Update(archetype, m_TypeIndex);
            var typeIndexInArchetype = m_Cache.IndexInArchetype;
            if (typeIndexInArchetype == -1) return false;
            var chunkVersion = archetype->Chunks.GetChangeVersion(typeIndexInArchetype, chunk.ListIndex);

            return ChangeVersionUtility.DidChange(chunkVersion, version);
        }

        public DynamicBuffer<T> this[Entity entity]
        {
            get
            {
                var ecs = m_Access->EntityComponentStore;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // Note that this check is only for the lookup table into the entity manager
                // The native array performs the actual read only / write only checks
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety0);
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                ecs->AssertEntityHasComponent(entity, m_TypeIndex, ref m_Cache);
#endif

                var header = (m_IsReadOnly != 0)?
                    (BufferHeader*)ecs->GetComponentDataWithTypeRO(entity, m_TypeIndex, ref m_Cache) :
                    (BufferHeader*)ecs->GetComponentDataWithTypeRW(entity, m_TypeIndex, m_GlobalSystemVersion, ref m_Cache);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new DynamicBuffer<T>(header, m_Safety0, m_ArrayInvalidationSafety, m_IsReadOnly != 0, false, 0, m_InternalCapacity);
#else
                return new DynamicBuffer<T>(header, m_InternalCapacity);
#endif
            }
        }

        public bool IsBufferEnabled(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Note that this check is only for the lookup table into the entity manager
            // The native array performs the actual read only / write only checks
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety0);
#endif
            return m_Access->IsComponentEnabled(entity, m_TypeIndex, ref m_Cache);
        }

        public void SetBufferEnabled(Entity entity, bool value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Note that this check is only for the lookup table into the entity manager
            // The native array performs the actual read only / write only checks
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety0);
#endif
            m_Access->SetComponentEnabled(entity, m_TypeIndex, value, ref m_Cache);
        }

        public void Update(SystemBase system)
        {
            Update(ref *system.m_StatePtr);
        }

        public void Update(ref SystemState systemState)
        {
            // NOTE: We could in theory fetch all this data from m_Access.EntityComponentStore and void the SystemState from being passed in.
            //       That would unfortunately allow this API to be called from a job. So we use the required system parameter as a way of signifying to the user that this can only be invoked from main thread system code.
            //       Additionally this makes the API symmetric to ComponentTypeHandle.
            m_GlobalSystemVersion =  systemState.m_EntityComponentStore->GlobalSystemVersion;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = &m_Access->DependencyManager->Safety;
            m_Safety0 = safetyHandles->GetSafetyHandleForComponentLookup(m_TypeIndex, m_IsReadOnly != 0);
            m_ArrayInvalidationSafety = safetyHandles->GetBufferHandleForBufferLookup(m_TypeIndex);
#endif
        }

        SafeBitRef MakeSafeBitRef(ulong* ptr, int offsetInBits)
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            => new SafeBitRef(ptr, offsetInBits, m_Safety0);
#else
            => new SafeBitRef(ptr, offsetInBits);
#endif
        
        public EnabledRefRW<T2> GetEnabledRefRW<T2>(Entity entity) where T2 : unmanaged, IEnableableComponent, IBufferElementData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety0);
#endif
            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex, ref m_Cache);

            int indexInBitField;
            int* ptrChunkDisabledCount;
            var ptr = ecs->GetEnabledRawRW(entity, m_TypeIndex, ref m_Cache, m_GlobalSystemVersion,
                out indexInBitField, out ptrChunkDisabledCount);

            return new EnabledRefRW<T2>(MakeSafeBitRef(ptr, indexInBitField), ptrChunkDisabledCount);
        }

        public EnabledRefRW<T2> GetEnabledRefRWOptional<T2>(Entity entity)
            where T2 : unmanaged, IBufferElementData, IEnableableComponent
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety0);
#endif
            if (!HasBuffer(entity))
                return new EnabledRefRW<T2>(default, default);

            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex, ref m_Cache);

            var ptr = ecs->GetEnabledRawRW(entity, m_TypeIndex, ref m_Cache, m_GlobalSystemVersion,
                out var indexInBitField, out var ptrChunkDisabledCount);

            return new EnabledRefRW<T2>(MakeSafeBitRef(ptr, indexInBitField), ptrChunkDisabledCount);
        }
        
        public EnabledRefRO<T2> GetEnabledRefRO<T2>(Entity entity) where T2 : unmanaged, IEnableableComponent, IBufferElementData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety0);
#endif
            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex, ref m_Cache);
            int indexInBitField;
            var ptr = ecs->GetEnabledRawRO(entity, m_TypeIndex, ref m_Cache, out indexInBitField, out _);
            return new EnabledRefRO<T2>(MakeSafeBitRef(ptr, indexInBitField));
        }

        public EnabledRefRO<T2> GetEnabledRefROOptional<T2>(Entity entity)
            where T2 : unmanaged, IBufferElementData, IEnableableComponent
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety0);
#endif
            if (!HasBuffer(entity))
                return new EnabledRefRO<T2>(default);

            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex, ref m_Cache);
            int indexInBitField;
            var ptr = ecs->GetEnabledRawRO(entity, m_TypeIndex, ref m_Cache, out indexInBitField, out _);
            return new EnabledRefRO<T2>(MakeSafeBitRef(ptr, indexInBitField));
        }
        
        public DynamicBuffer<T> GetBuffer(Entity entity, bool bumpVersion)
        {
            var ecs = m_Access->EntityComponentStore;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Note that this check is only for the lookup table into the entity manager
            // The native array performs the actual read only / write only checks
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety0);
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            ecs->AssertEntityHasComponent(entity, m_TypeIndex, ref m_Cache);
#endif

            var header = bumpVersion
                ? (BufferHeader*)ecs->GetComponentDataWithTypeRW(entity, m_TypeIndex, m_GlobalSystemVersion, ref m_Cache)
                : (BufferHeader*)ecs->GetComponentDataWithTypeRO(entity, m_TypeIndex, ref m_Cache);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new DynamicBuffer<T>(header, m_Safety0, m_ArrayInvalidationSafety, m_IsReadOnly != 0, false, 0, m_InternalCapacity);
#else
                return new DynamicBuffer<T>(header, m_InternalCapacity);
#endif
        }
        
        public void SetChangeVersion(Entity entity)
        {
            var ecs = m_Access->EntityComponentStore;
            var chunk = ecs->GetChunk(entity);
            var archetype = ecs->GetArchetype(chunk);

            if (Hint.Unlikely(archetype != m_Cache.Archetype))
                m_Cache.Update(archetype, m_TypeIndex);

            archetype->Chunks.SetChangeVersion(m_Cache.IndexInArchetype, chunk.ListIndex, m_GlobalSystemVersion);
        }
        
        public static implicit operator UnsafeBufferLookup<T>(BufferLookup<T> lookup)
        {
            return UnsafeUtility.As<BufferLookup<T>, UnsafeBufferLookup<T>>(ref lookup);
        }
    }
}