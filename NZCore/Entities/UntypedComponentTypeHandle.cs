// <copyright project="NZCore" file="UntypedComponentTypeHandle.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public struct UntypedComponentTypeHandle
    {
        internal LookupCache _lookupCache;
        internal readonly TypeIndex _typeIndex;
        internal uint _globalSystemVersion;
        internal readonly byte _isReadOnly;
        internal readonly byte _isZeroSized;

        public readonly uint GlobalSystemVersion => _globalSystemVersion;
        public readonly bool IsReadOnly => _isReadOnly == 1;
        internal readonly bool IsZeroSized => _isZeroSized == 1;

#pragma warning disable 0414
        private readonly int m_Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private readonly int m_MinIndex;
        private readonly int m_MaxIndex;
        internal  AtomicSafetyHandle m_Safety0;
        internal  AtomicSafetyHandle m_Safety1;
        internal readonly int m_SafetyReadOnlyCount;
        internal readonly int m_SafetyReadWriteCount;
#endif
#pragma warning restore 0414

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal UntypedComponentTypeHandle(ComponentType componentType, AtomicSafetyHandle safety0, AtomicSafetyHandle safety1, 
            uint globalSystemVersion)
#else
        internal UntypedComponentTypeHandle(ComponentType componentType, uint globalSystemVersion)
#endif
        {
            var typeIndex = componentType.TypeIndex;
            var typeInfo = TypeManager.GetTypeInfo(typeIndex);

            m_Length = 1;
            _typeIndex = typeIndex;
            _isZeroSized = typeInfo.IsZeroSized ? (byte)1u : (byte)0u;
            _globalSystemVersion = globalSystemVersion;
            _isReadOnly = componentType.AccessModeType == ComponentType.AccessMode.ReadOnly ? (byte)1u : (byte)0u;
            _lookupCache = new LookupCache();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_MinIndex = 0;
            m_MaxIndex = 0;
            m_Safety0 = safety0;
            m_Safety1 = safety1;
            int numHandles = componentType.IsBuffer ? 2 : 1;
            m_SafetyReadOnlyCount = _isReadOnly == 1 ? numHandles : 0;
            m_SafetyReadWriteCount = _isReadOnly == 1 ? 0: numHandles;
#endif
        }

        public unsafe void Update(SystemBase system)
        {
            Update(ref *system.m_StatePtr);
        }
        
        public unsafe void Update(ref SystemState state)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety0 = state.m_DependencyManager->Safety.GetSafetyHandleForDynamicComponentTypeHandle(_typeIndex, IsReadOnly);
            int numHandles = IsReadOnly ? m_SafetyReadOnlyCount : m_SafetyReadWriteCount;
            if (numHandles > 1)
                m_Safety1 = state.m_DependencyManager->Safety.GetBufferHandleForBufferTypeHandle(_typeIndex);
#endif
            _globalSystemVersion = state.GlobalSystemVersion;
        }

        public FixedString512Bytes ToFixedString()
        {
            var fs = new FixedString128Bytes((FixedString32Bytes)"UntypedComponentTypeHandle[");
            fs.Append(TypeManager.GetTypeNameFixed(_typeIndex));
            fs.Append(']');
            return fs;
        }
    }

    public static unsafe class UntypedComponentTypeHandleExtensions
    {
        public static UntypedComponentTypeHandle GetUntypedHandle(this ref SystemState state, ComponentType componentType)
        {
            state.AddReaderWriter(componentType);
            return GetUntypedComponentTypeHandle(ref state, componentType);
        }
        
        private static UntypedComponentTypeHandle GetUntypedComponentTypeHandle(ref SystemState state, ComponentType componentType)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var access = state.EntityManager.GetCheckedEntityDataAccess();
            if (!componentType.IsBuffer)
            {
                return new UntypedComponentTypeHandle(componentType,
                    access->DependencyManager->Safety.GetSafetyHandleForDynamicComponentTypeHandle(componentType.TypeIndex, componentType.AccessModeType == ComponentType.AccessMode.ReadOnly),
                    default(AtomicSafetyHandle), state.GlobalSystemVersion);
            }
            else
            {
                return new UntypedComponentTypeHandle(componentType,
                    access->DependencyManager->Safety.GetSafetyHandleForDynamicComponentTypeHandle(componentType.TypeIndex, componentType.AccessModeType == ComponentType.AccessMode.ReadOnly),
                    access->DependencyManager->Safety.GetBufferHandleForBufferTypeHandle(componentType.TypeIndex),
                    state.GlobalSystemVersion);
            }

#else
            return new UntypedComponentTypeHandle(componentType, GlobalSystemVersion);
#endif
        }
        
        
        public static byte* GetComponentDataRawRO(this in ArchetypeChunk chunk, ref UntypedComponentTypeHandle untypedHandle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(untypedHandle.m_Safety0);
#endif

            byte* ptr = ChunkDataUtility.GetComponentDataWithTypeRO(chunk.m_Chunk, chunk.Archetype.Archetype, 0,
                untypedHandle._typeIndex, ref untypedHandle._lookupCache);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            // Must check this after computing the pointer, to make sure the cache is up to date
            if (Hint.Unlikely(untypedHandle._lookupCache.IndexInArchetype == -1))
            {
                var typeName = untypedHandle._typeIndex.ToFixedString();
                throw new ArgumentException($"Required component {typeName} not found in archetype.");
            }
#endif
            return ptr;
        }

        public static byte* GetComponentDataRawRW(this in ArchetypeChunk chunk, ref UntypedComponentTypeHandle untypedHandle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(untypedHandle.m_Safety0);
#endif

            byte* ptr = ChunkDataUtility.GetComponentDataWithTypeRW(chunk.m_Chunk, chunk.Archetype.Archetype, 0,
                untypedHandle._typeIndex, untypedHandle.GlobalSystemVersion, ref untypedHandle._lookupCache);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(untypedHandle.IsReadOnly))
                throw new InvalidOperationException(
                    "Provided UntypedComponentTypeHandle is read-only; can't get a read/write pointer to component data");
            // Must check this after computing the pointer, to make sure the cache is up to date
            if (Hint.Unlikely(untypedHandle._lookupCache.IndexInArchetype == -1))
            {
                var typeName = untypedHandle._typeIndex.ToFixedString();
                throw new ArgumentException($"Required component {typeName} not found in archetype.");
            }
#endif
            // todo, add journaling?
            return ptr;
        }
    }
}