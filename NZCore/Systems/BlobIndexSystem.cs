// <copyright project="NZCore" file="BlobIndexSystem.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
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
        protected override void OnCreate()
        {
            RequireForUpdate<TBlobReference>();
        }

        protected override void OnStartRunning()
        {
            CheckedStateRef.CreateSingleton<TIndexList>(out var singleton);

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TBlobReference>()
                .Build(ref CheckedStateRef);

            new BlobIndexJob()
            {
                List = singleton.GetIndexList(),
                BlobReference_ReadHandle = SystemAPI.GetComponentTypeHandle<TBlobReference>()
            }.Run(query);
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