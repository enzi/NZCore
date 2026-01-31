// <copyright project="NZCore" file="BlobIndexSystem.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.Interfaces;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace NZCore
{
    public partial class BlobIndexSystem<TIndexList, TBlobReference, TBlobRoot> : SystemBase
        where TIndexList : unmanaged, IInitSingleton, IDisposable, IBlobIndexList<TBlobReference>
        where TBlobReference : unmanaged, IBlobIndex, IBlobAssetReference<TBlobRoot>
        where TBlobRoot : unmanaged
    {
        private EntityQuery _query;

        protected override void OnCreate()
        {
            _query = new EntityQueryBuilder(Allocator.Temp)
                        .WithAll<TBlobReference>()
                        .WithOptions(EntityQueryOptions.IncludePrefab)
                        .Build(ref CheckedStateRef);
            
            RequireForUpdate(_query);
        }

        protected override void OnStartRunning()
        {
            CheckedStateRef.CreateSingleton(out TIndexList singleton);

            new BlobIndexJob()
            {
                List = singleton.GetIndexList(),
                BlobReference_ReadHandle = SystemAPI.GetComponentTypeHandle<TBlobReference>()
            }.Run(_query);
        }

        protected override void OnStopRunning()
        {
            CheckedStateRef.DisposeSingleton<TIndexList>();
        }

        protected override void OnUpdate()
        {
        }

        private unsafe struct BlobIndexJob : IJobChunk
        {
            public NativeList<TBlobReference> List;
            public ComponentTypeHandle<TBlobReference> BlobReference_ReadHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var blobRefs = (TBlobReference*)chunk.GetRequiredComponentDataPtrRO(ref BlobReference_ReadHandle);

                int highestIndex = 0;

                for (int i = 0; i < chunk.Count; i++)
                {
                    var blobRef = blobRefs[i];

                    int blobIndex = blobRef.BlobIndex;

                    if (blobIndex > highestIndex)
                        highestIndex = blobIndex;
                }

                List.Resize(highestIndex + 1, NativeArrayOptions.ClearMemory);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var blobRef = blobRefs[i];

                    List[blobRef.BlobIndex] = new TBlobReference
                    {
                        blob = blobRef.blob
                    };
                }
            }
        }
    }
}