// <copyright project="NZCore" file="BlobPatchSystem.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;

namespace NZCore
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SceneSystemGroup))]
    public partial struct BlobPatchSystem : ISystem
    {
        private EntityQuery _patchObjectRefquery;
        private EntityQuery _patchEntityRefquery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _patchObjectRefquery = SystemAPI.QueryBuilder()
                                            .WithAll<UnityObjectReferencePatchBuffer>()
                                            .WithDisabled<UnityObjectReferencePatchBufferResolved>()
                                            .Build();
            
            _patchEntityRefquery = SystemAPI.QueryBuilder()
                                            .WithAll<EntityRefPatchBuffer>()
                                            .WithDisabled<EntityRefPatchBufferResolved>()
                                            .Build();
        }

        [BurstCompile]
        public unsafe void OnUpdate(ref SystemState state)
        {
            if (_patchEntityRefquery.IsEmpty)
            {
                var entities = _patchEntityRefquery.ToEntityArray(Allocator.Temp);

                foreach (var entity in entities)
                {
                    var buffer = SystemAPI.GetBuffer<EntityRefPatchBuffer>(entity);
                    
                    foreach (var element in buffer)
                    {
                        if (element.EntityToPatch == Entity.Null || element.TypeIndex == default || element.BlobOffset == 0)
                        {
                            continue;
                        }

                        var ptr = (BlobAssetReference<byte>*)state.EntityManager.GetComponentDataRawRW(element.BlobEntity, element.TypeIndex);
                        var blobPtr = (byte*)(ptr + element.BlobAssetReferenceIndex)->GetUnsafePtr();

                        // patch the Entity reference on a blob
                        *((Entity*)(blobPtr + element.BlobOffset)) = element.EntityToPatch;
                    }

                    state.EntityManager.SetComponentEnabled<EntityRefPatchBufferResolved>(entity, true);
                }
            }

            if (!_patchObjectRefquery.IsEmpty)
            {
                var entities = _patchObjectRefquery.ToEntityArray(Allocator.Temp);

                foreach (var entity in entities)
                {
                    var buffer = SystemAPI.GetBuffer<UnityObjectReferencePatchBuffer>(entity);

                    foreach (var element in buffer)
                    {
                        if (element.BlobEntity == Entity.Null || element.TypeIndex == default || element.BlobOffset == 0)
                        {
                            continue;
                        }

                        var ptr = (BlobAssetReference<byte>*)state.EntityManager.GetComponentDataRawRW(element.BlobEntity, element.TypeIndex);
                        var blobPtr = (byte*)(ptr + element.BlobAssetReferenceIndex)->GetUnsafePtr();

                        // patch the instanceId so any UnityObjectRefForBlob<T> can be resolved correctly
                        var newInstanceId = element.Asset.GetInstanceId();
                        *((int*)(blobPtr + element.BlobOffset)) = newInstanceId;
                    }

                    state.EntityManager.SetComponentEnabled<UnityObjectReferencePatchBufferResolved>(entity, true);
                }
            }
        }
    }
}