// <copyright project="Unity.Entities" file="EntityInternals.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using UnityEntities = Unity.Entities;
using Object = UnityEngine.Object;

namespace NZCore.Internal
{
    // Direct calls only: Unity owns the implementations, including their safety and change-version behavior.
    // Accessors use their own names so they never bind to Unity's members where those are still visible (package Entities).
    public static unsafe class EntityInternals
    {
        public const int kChunkSize = Chunk.kChunkSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChunkIndex GetChunkIndex(this in ArchetypeChunk chunk) => ChunkIndex.From(chunk.m_Chunk);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetChunkIndex(this ref ArchetypeChunk chunk, ChunkIndex chunkIndex) => chunk.m_Chunk = chunkIndex.ToUnity();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EntityComponentStore* GetEntityComponentStore(this in ArchetypeChunk chunk) => (EntityComponentStore*)chunk.m_EntityComponentStore;

        public static ArchetypeChunk CreateArchetypeChunk(ChunkIndex chunk, EntityComponentStore* entityComponentStore) =>
            new(chunk.ToUnity(), (UnityEntities.EntityComponentStore*)entityComponentStore);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Archetype* GetArchetype(this in EntityArchetype archetype) => (Archetype*)archetype.Archetype;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref LookupCache GetLookupCache<T>(this ref ComponentTypeHandle<T> handle) =>
            ref UnsafeUtility.As<UnityEntities.LookupCache, LookupCache>(ref handle.m_LookupCache);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TypeIndex GetTypeIndex<T>(this in ComponentTypeHandle<T> handle) => handle.m_TypeIndex;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref LookupCache GetLookupCache<T>(this ref BufferTypeHandle<T> handle)
            where T : unmanaged, IBufferElementData =>
            ref UnsafeUtility.As<UnityEntities.LookupCache, LookupCache>(ref handle.m_LookupCache);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TypeIndex GetTypeIndex<T>(this in BufferTypeHandle<T> handle)
            where T : unmanaged, IBufferElementData => handle.m_TypeIndex;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly TypeIndex GetTypeIndex(ref DynamicComponentTypeHandle handle) => ref handle.m_TypeIndex;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AtomicSafetyHandle GetSafety<T>(this in ComponentTypeHandle<T> handle) => handle.m_Safety;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AtomicSafetyHandle GetSafety0<T>(this in BufferTypeHandle<T> handle)
            where T : unmanaged, IBufferElementData => handle.m_Safety0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AtomicSafetyHandle GetSafety1<T>(this in BufferTypeHandle<T> handle)
            where T : unmanaged, IBufferElementData => handle.m_Safety1;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public static ComponentLookup<T> CreateComponentLookup<T>(TypeIndex typeIndex, EntityDataAccess* access, bool isReadOnly)
            where T : unmanaged, IComponentData =>
            new(typeIndex, (UnityEntities.EntityDataAccess*)access, isReadOnly);

        public static DynamicBuffer<T> CreateDynamicBuffer<T>(BufferHeader* header, AtomicSafetyHandle safety, AtomicSafetyHandle arrayInvalidationSafety,
            bool isReadOnly, bool useMemoryInitPattern, byte memoryInitPattern, int internalCapacity)
            where T : unmanaged =>
            new((UnityEntities.BufferHeader*)header, safety, arrayInvalidationSafety, isReadOnly, useMemoryInitPattern, memoryInitPattern, internalCapacity);

        public static BufferAccessor<T> CreateBufferAccessor<T>(byte* basePointer, int length, int stride, bool readOnly, AtomicSafetyHandle safety,
            AtomicSafetyHandle arrayInvalidationSafety, int internalCapacity)
            where T : unmanaged, IBufferElementData =>
            new(basePointer, length, stride, readOnly, safety, arrayInvalidationSafety, internalCapacity);

        public static RefRO<T> CreateRefRO<T>(void* ptr, AtomicSafetyHandle safety)
            where T : struct, IComponentData => new(ptr, safety);

        public static RefRW<T> CreateRefRW<T>(void* ptr, AtomicSafetyHandle safety)
            where T : struct, IComponentData => new(ptr, safety);
#else
        public static ComponentLookup<T> CreateComponentLookup<T>(TypeIndex typeIndex, EntityDataAccess* access)
            where T : unmanaged, IComponentData =>
            new(typeIndex, (UnityEntities.EntityDataAccess*)access);

        public static DynamicBuffer<T> CreateDynamicBuffer<T>(BufferHeader* header, int internalCapacity)
            where T : unmanaged =>
            new((UnityEntities.BufferHeader*)header, internalCapacity);

        public static BufferAccessor<T> CreateBufferAccessor<T>(byte* basePointer, int length, int stride, int internalCapacity)
            where T : unmanaged, IBufferElementData =>
            new(basePointer, length, stride, internalCapacity);

        public static RefRO<T> CreateRefRO<T>(void* ptr)
            where T : struct, IComponentData => new(ptr);

        public static RefRW<T> CreateRefRW<T>(void* ptr)
            where T : struct, IComponentData => new(ptr);
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Entity GetEntity(this in SystemHandle handle) => handle.m_Entity;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SystemState* GetStatePtr(this ComponentSystemBase system) => system.m_StatePtr;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EntityComponentStore* GetEntityComponentStore(this ref SystemState state) => (EntityComponentStore*)state.m_EntityComponentStore;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentDependencyManager* GetDependencyManager(this ref SystemState state) => (ComponentDependencyManager*)state.m_DependencyManager;

        public static JobHandle GetJobHandle(this ref SystemState state) => state.m_JobHandle;

        public static UnsafeList<TypeIndex> GetJobDependencyForReadingSystems(this ref SystemState state) => state.m_JobDependencyForReadingSystems;

        public static UnsafeList<TypeIndex> GetJobDependencyForWritingSystems(this ref SystemState state) => state.m_JobDependencyForWritingSystems;

        public static ref UnsafeList<EntityQuery> GetEntityQueries(ref SystemState state) => ref state.EntityQueries;

        public static ref UnsafeList<EntityQuery> GetRequiredEntityQueries(ref SystemState state) => ref state.RequiredEntityQueries;

        public static ComponentSystemBase GetManagedSystem(ref SystemState state) => state.ManagedSystem;

        public static string GetProfilerMarkerName(ref SystemState state, World world) => state.GetProfilerMarkerName(world);

        public static void AddReaderWriter(ref SystemState state, ComponentType componentType) => state.AddReaderWriter(componentType);

        public static EntityDataAccess* GetCheckedEntityDataAccess(EntityManager entityManager) =>
            (EntityDataAccess*)entityManager.GetCheckedEntityDataAccess();

        public static void* GetComponentDataRawRO(EntityManager entityManager, Entity entity, TypeIndex typeIndex) =>
            entityManager.GetComponentDataRawRO(entity, typeIndex);

        public static void* GetComponentDataRawRW(EntityManager entityManager, Entity entity, TypeIndex typeIndex) =>
            entityManager.GetComponentDataRawRW(entity, typeIndex);

        public static void* GetBufferRawRO(EntityManager entityManager, Entity entity, TypeIndex typeIndex) => entityManager.GetBufferRawRO(entity, typeIndex);

        public static int GetBufferLength(EntityManager entityManager, Entity entity, TypeIndex typeIndex) => entityManager.GetBufferLength(entity, typeIndex);

        public static SystemHandle GetExecutingSystem(WorldUnmanaged world) => world.ExecutingSystem;

        public static Type GetTypeOfSystem(WorldUnmanaged world, SystemHandle systemHandle) => world.GetTypeOfSystem(systemHandle);

        public static SystemState* ResolveSystemStateChecked(WorldUnmanaged world, SystemHandle systemHandle) => world.ResolveSystemStateChecked(systemHandle);

        public static FixedString128Bytes GetTypeNameFixed(TypeIndex typeIndex) => TypeManager.GetTypeNameFixed(typeIndex);

        public static Type UnityEngineObjectType => TypeManager.UnityEngineObjectType;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EntityQueryImpl* GetImpl(this EntityQuery query) => (EntityQueryImpl*)query._GetImpl();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EntityQueryImpl* GetImplUnchecked(this EntityQuery query) => (EntityQueryImpl*)query.__impl;

        public static int GetHeaderLength<T>(this in BlobAssetReference<T> blob)
            where T : unmanaged => blob.m_data.Header->Length;

        public static void* GetHeaderValidationPtr<T>(this in BlobAssetReference<T> blob)
            where T : unmanaged => blob.m_data.Header->ValidationPtr;

        public static UntypedUnityObjectRef GetId<T>(this in UnityObjectRef<T> objectRef)
            where T : Object => UntypedUnityObjectRef.From(objectRef.Id);

        public static UnityObjectRef<T> CreateUnityObjectRef<T>(UntypedUnityObjectRef id)
            where T : Object => new() { Id = id.ToUnity() };
    }
}