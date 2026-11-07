// <copyright project="NZCore" file="PrefabInstanceSpawnerSystem.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using NZCore.Internal;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using BufferHeader = NZCore.Internal.BufferHeader;

namespace NZCore.AssetManagement
{
    [UpdateInGroup(typeof(InstantiateSystemGroup))]
    public partial struct PrefabInstanceSpawnerSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder().WithAll<PrefabInstanceSpawner>().Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var spawners = _query.ToEntityArray(Allocator.Temp);

            foreach (var spawner in spawners)
            {
                var data = entityManager.GetComponentData<PrefabInstanceSpawner>(spawner);
                var instance = entityManager.Instantiate(data.Prefab);
                entityManager.SetComponentData(instance, data.Transform);
                PrefabSpawnerOverrides.Copy(entityManager, spawner, instance);
            }

            entityManager.DestroyEntity(spawners);
        }
    }

#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    internal static unsafe partial class PrefabSpawnerOverrides
    {
        private static readonly Dictionary<TypeIndex, bool> IsOverrideByType = new();

        public static void Copy(EntityManager entityManager, Entity spawner, Entity instance)
        {
            var types = entityManager.GetComponentTypes(spawner, Allocator.Temp);
            var overrides = new NativeList<ComponentType>(types.Length, Allocator.Temp);

            foreach (var type in types)
            {
                if (!IsOverride(type))
                {
                    continue;
                }

                if (type.IsSharedComponent || type.IsManagedComponent || type.IsChunkComponent)
                {
                    Debug.LogError($"{entityManager.GetName(spawner)} has the override {TypeManager.GetType(type.TypeIndex).Name}, " +
                                   "but a spawner can only copy unmanaged components and buffers.");
                    continue;
                }

                overrides.Add(type);
            }

            foreach (var type in overrides)
            {
                if (!entityManager.HasComponent(instance, type))
                {
                    entityManager.AddComponent(instance, type);
                }
            }

            foreach (var type in overrides)
            {
                var typeInfo = TypeManager.GetTypeInfo(type.TypeIndex);

                if (type.IsBuffer)
                {
                    var length = EntityInternals.GetBufferLength(entityManager, spawner, type.TypeIndex);
                    var source = EntityInternals.GetBufferRawRO(entityManager, spawner, type.TypeIndex);
                    var header = (BufferHeader*)EntityInternals.GetComponentDataRawRW(entityManager, instance, type.TypeIndex);

                    BufferHeader.EnsureCapacity(header, length, typeInfo.ElementSize, typeInfo.AlignmentInBytes, BufferHeader.TrashMode.TrashOldData, false, 0);
                    header->Length = length;
                    UnsafeUtility.MemCpy(BufferHeader.GetElementPointer(header), source, (long)length * typeInfo.ElementSize);
                }
                else if (!type.IsZeroSized)
                {
                    UnsafeUtility.MemCpy(EntityInternals.GetComponentDataRawRW(entityManager, instance, type.TypeIndex),
                        EntityInternals.GetComponentDataRawRO(entityManager, spawner, type.TypeIndex), typeInfo.TypeSize);
                }

                if (type.IsEnableable)
                {
                    entityManager.SetComponentEnabled(instance, type, entityManager.IsComponentEnabled(spawner, type));
                }
            }
        }

        private static bool IsOverride(ComponentType type)
        {
            if (type.TypeIndex == TypeManager.GetTypeIndex<PrefabInstanceSpawner>())
            {
                return false;
            }

            if (!IsOverrideByType.TryGetValue(type.TypeIndex, out var isOverride))
            {
                var managedType = TypeManager.GetType(type.TypeIndex);
                isOverride = managedType != null && !managedType.Assembly.GetName().Name.StartsWith("Unity", StringComparison.Ordinal);
                IsOverrideByType[type.TypeIndex] = isOverride;
            }

            return isOverride;
        }
    }
}
