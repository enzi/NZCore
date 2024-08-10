// <copyright project="NZCore" file="SharedComponentLookup.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    [NativeContainer]
    public unsafe struct SharedComponentLookup<T> where T : unmanaged, ISharedComponentData
    {
        [NativeDisableUnsafePtrRestriction] private readonly EntityDataAccess* m_Access;
        private readonly TypeIndex m_TypeIndex;
        private LookupCache m_Cache;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private readonly byte m_IsReadOnly;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal SharedComponentLookup(TypeIndex typeIndex, EntityDataAccess* access, bool isReadOnly)
        {
            var safetyHandles = &access->DependencyManager->Safety;
            m_Safety = safetyHandles->GetSafetyHandleForComponentLookup(typeIndex, isReadOnly);
            m_IsReadOnly = isReadOnly ? (byte)1 : (byte)0;
            m_TypeIndex = typeIndex;
            m_Access = access;
            m_Cache = default;
        }
#else
        internal SharedComponentLookup(TypeIndex typeIndex, EntityDataAccess* access)
        {
            m_TypeIndex = typeIndex;
            m_Access = access;
            m_Cache = default;
        }
#endif

        public T this[Entity entity]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return m_Access->GetSharedComponentData_Unmanaged<T>(entity);
            }
        }

        // access by sharedCompIndex
        // probably very rare but less overhead than by entity
        // so it could be cached
        public T this[int sharedComponentIndex]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return m_Access->GetSharedComponentData_Unmanaged<T>(sharedComponentIndex);
            }
        }

        public bool HasComponent(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            var ecs = m_Access->EntityComponentStore;
            return ecs->HasComponent(entity, m_TypeIndex, ref m_Cache);
        }

        public bool TryGetComponent(Entity entity, out T sharedComp)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            if (!HasComponent(entity))
            {
                sharedComp = default;
                return false;
            }

            var ecs = m_Access->EntityComponentStore;
            ecs->AssertEntityHasComponent(entity, m_TypeIndex);

            sharedComp = m_Access->GetSharedComponentData_Unmanaged<T>(entity);
            return true;
        }

        public void Update(SystemBase system)
        {
            Update(ref *system.m_StatePtr);
        }

        public void Update(ref SystemState systemState)
        {
            // do we need a global system version?
            //m_GlobalSystemVersion = systemState.m_EntityComponentStore->GlobalSystemVersion;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = &m_Access->DependencyManager->Safety;
            m_Safety = safetyHandles->GetSafetyHandleForComponentLookup(m_TypeIndex, m_IsReadOnly != 0);
#endif
        }
    }
}