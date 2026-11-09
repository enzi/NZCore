// <copyright project="NZCore" file="PrefabDefinitionOverrides.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.Internal;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using BufferHeader = NZCore.Internal.BufferHeader;

namespace NZCore.AssetManagement
{
    public static unsafe class PrefabDefinitionOverrides
    {
        public static void Apply(EntityManager entityManager, NativeHashSet<TypeIndex> defaultTypes, Entity spawner, Entity overrides, Entity instance)
        {
            var types = entityManager.GetComponentTypes(overrides, Allocator.Temp);

            foreach (var type in types)
            {
                var typeIndex = type.TypeIndex;

                if (defaultTypes.Contains(typeIndex))
                {
                    continue;
                }

                if (typeIndex.IsSharedComponentType || typeIndex.IsManagedType || typeIndex.IsChunkComponent)
                {
                    entityManager.GetName(spawner, out var spawnerName);
                    Debug.LogError(
                        $"{spawnerName} skips the override {EntityInternals.GetTypeNameFixed(typeIndex)}, because a spawner copies only unmanaged components and buffers.");
                    continue;
                }

                var componentType = ComponentType.FromTypeIndex(typeIndex);
                entityManager.AddComponent(instance, componentType);
                var typeInfo = TypeManager.GetTypeInfo(typeIndex);

                if (typeIndex.IsBuffer)
                {
                    var length = EntityInternals.GetBufferLength(entityManager, overrides, typeIndex);
                    var source = EntityInternals.GetBufferRawRO(entityManager, overrides, typeIndex);
                    var header = (BufferHeader*)EntityInternals.GetComponentDataRawRW(entityManager, instance, typeIndex);

                    BufferHeader.EnsureCapacity(header, length, typeInfo.ElementSize, typeInfo.AlignmentInBytes, BufferHeader.TrashMode.TrashOldData, false, 0);
                    header->Length = length;
                    UnsafeUtility.MemCpy(BufferHeader.GetElementPointer(header), source, (long)length * typeInfo.ElementSize);
                }
                else if (!typeIndex.IsZeroSized)
                {
                    UnsafeUtility.MemCpy(EntityInternals.GetComponentDataRawRW(entityManager, instance, typeIndex),
                        EntityInternals.GetComponentDataRawRO(entityManager, overrides, typeIndex), typeInfo.TypeSize);
                }

                if (typeIndex.IsEnableable)
                {
                    entityManager.SetComponentEnabled(instance, componentType, entityManager.IsComponentEnabled(overrides, componentType));
                }
            }
        }
    }
}