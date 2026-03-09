// <copyright project="NZCore" file="BlobIndexSystem.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using NZCore.Interfaces;

namespace NZCore
{
    public partial class BlobIndexListSystem<TIndexList, TBlobReference, TBlobRoot> : SystemBase
        where TIndexList : unmanaged, IInitSingleton, IDisposable, IBlobIndexList<TBlobReference>
        where TBlobReference : unmanaged, IBlobIndex, IBlobAssetReference<TBlobRoot>
        where TBlobRoot : unmanaged
    {
        private EntityQuery _query;

        protected override void OnCreate()
        {
            _query = new EntityQueryBuilder(Allocator.Temp)
                        .WithAll<Prefab, TBlobReference>()
                        .WithOptions(EntityQueryOptions.IncludePrefab)
                        .Build(ref CheckedStateRef);
            
            RequireForUpdate(_query);
        }

        [BurstCompile]
        protected override void OnStartRunning()
        {
            CheckedStateRef.CreateSingleton(out TIndexList singleton);

            new BlobIndexListJob()
            {
                List = singleton.GetIndexList(),
                BlobReference_ReadHandle = SystemAPI.GetComponentTypeHandle<TBlobReference>()
            }.Run(_query);
        }

        protected override void OnStopRunning()
        {
            CheckedStateRef.DisposeSingleton<TIndexList>();
        }

        protected override void OnUpdate() { }

        [BurstCompile]
        private unsafe struct BlobIndexListJob : IJobChunk
        {
            public NativeList<TBlobReference> List;
            public ComponentTypeHandle<TBlobReference> BlobReference_ReadHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                //Debug.Log($"BlobIndexJob for {typeof(TBlobReference).Name}");
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
    
    public partial class BlobIndexMapSystem<TIndexList, TBlobReference, TBlobRoot> : SystemBase
        where TIndexList : unmanaged, IInitSingleton, IDisposable, IBlobIndexMap<TBlobReference>
        where TBlobReference : unmanaged, IBlobIndex, IBlobAssetReference<TBlobRoot>
        where TBlobRoot : unmanaged
    {
        private EntityQuery _query;

        protected override void OnCreate()
        {
            _query = new EntityQueryBuilder(Allocator.Temp)
                        .WithAll<Prefab, TBlobReference>()
                        .WithOptions(EntityQueryOptions.IncludePrefab)
                        .Build(ref CheckedStateRef);
            
            RequireForUpdate(_query);
        }

        [BurstCompile]
        protected override void OnStartRunning()
        {
            CheckedStateRef.CreateSingleton(out TIndexList singleton);

            new BlobIndexMapJob()
            {
                Map = singleton.GetMap(),
                BlobReference_ReadHandle = SystemAPI.GetComponentTypeHandle<TBlobReference>()
            }.Run(_query);
        }

        protected override void OnStopRunning()
        {
            CheckedStateRef.DisposeSingleton<TIndexList>();
        }

        protected override void OnUpdate() { }

        [BurstCompile]
        private unsafe struct BlobIndexMapJob : IJobChunk
        {
            public NativeHashMap<int, TBlobReference> Map;
            public ComponentTypeHandle<TBlobReference> BlobReference_ReadHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                //Debug.Log($"BlobIndexJob for {typeof(TBlobReference).Name}");
                var blobRefs = (TBlobReference*)chunk.GetRequiredComponentDataPtrRO(ref BlobReference_ReadHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var blobRef = blobRefs[i];

                    Map.TryAdd(blobRef.BlobIndex, new TBlobReference()
                    {
                        blob = blobRef.blob
                    });
                }
            }
        }
    }
}