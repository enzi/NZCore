// <copyright project="NZCore.Editor" file="ScriptableObjectConverter_Baker.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NZCore
{
    public interface IConvertToBlob
    {
        public void Bake(IBaker baker);
    }
    
    public interface IConvertToBlob<TSoClass, TBlobReference, TBlobStruct> : IConvertToBlob<TBlobStruct>
        where TSoClass : ScriptableObject, IConvertToBlob<TBlobStruct>
        where TBlobStruct : unmanaged
        where TBlobReference : unmanaged, IComponentData, IBlobAssetReference<TBlobStruct>
    {
        void IConvertToBlob.Bake(IBaker baker)
        {
            GenericBlobBaker.Bake<TSoClass, TBlobReference, TBlobStruct>(baker);
        }
    }
    
    public interface IConvertToBlob<TSoClass, TBlobReference, TBlobStruct1, TBlobStruct2> : IConvertToBlob<TBlobStruct1, TBlobStruct2>
        where TSoClass : ScriptableObject, IConvertToBlob<TBlobStruct1, TBlobStruct2>
        where TBlobStruct1 : unmanaged
        where TBlobStruct2 : unmanaged
        where TBlobReference : unmanaged, IComponentData, IBlobAssetReference<TBlobStruct1, TBlobStruct2>
    {
        void IConvertToBlob.Bake(IBaker baker)
        {
            GenericBlobBaker.Bake<TSoClass, TBlobReference, TBlobStruct1, TBlobStruct2>(baker);
        }
    }

    public interface IConvertToBlob<T> : IConvertToBlob
        where T : unmanaged
    {
        public void ToBlobData(GenericBlobBaker.ContextBase context, ref BlobBuilder blobBuilder, ref T blob);
    }

    // 2 blobs
    public interface IConvertToBlob<T1, T2> : IConvertToBlob
        where T1 : unmanaged
        where T2 : unmanaged
    {
        public void ToBlobData(GenericBlobBaker.ContextBase context, ref BlobBuilder blobBuilder, ref T1 blob1, ref T2 blob2);
    }
    
    public static class GenericBlobBaker
    {
        public abstract class ContextBase
        {
            public IBaker Baker;
            public Entity BlobEntity;
            public Guid AssetGuid;
            public DynamicBuffer<WeakReferenceAssetBuffer> AssetBuffer;
            internal DynamicBuffer<UnityObjectReferencePatchBuffer> ObjectRefBuffer;
            internal DynamicBuffer<EntityRefPatchBuffer> EntityRefBuffer;
            public unsafe byte*[] BlobAddress;

            /// <summary>
            /// provide the asset (like Sprite), ref to the blob field you want to patch
            /// and blobAssetReferenceIndex which is usually 0 unless you have more than one blob in an IBlobAssetReference
            /// </summary>
            public abstract void AddObjectRef<T>(T asset, ref UnityObjectRefForBlob<T> blobField, int blobAssetReferenceIndex = 0) where T : Object;

            /// <summary>
            /// provide the entity that needs to be patched, ref to the blob field you want to patch
            /// and blobAssetReferenceIndex which is usually 0 unless you have more than one blob in an IBlobAssetReference
            /// </summary>
            public abstract void AddEntityRef(Entity entity, ref Entity blobField, int blobAssetReferenceIndex = 0);
        }

        private class Context<TBlobReference> : ContextBase

            where TBlobReference : unmanaged, IComponentData
        {
            public override unsafe void AddObjectRef<T>(T asset, ref UnityObjectRefForBlob<T> blobField, int blobAssetReferenceIndex = 0)
            {
                ObjectRefBuffer.Add(new UnityObjectReferencePatchBuffer()
                {
                    TypeIndex = TypeManager.GetTypeIndex<TBlobReference>(),
                    BlobEntity = BlobEntity,
                    Asset = asset,
                    BlobOffset = (byte*) UnsafeUtility.AddressOf(ref blobField) - BlobAddress[blobAssetReferenceIndex],
                    BlobAssetReferenceIndex = blobAssetReferenceIndex
                });
            }
            
            public override unsafe void AddEntityRef(Entity entity, ref Entity blobField, int blobAssetReferenceIndex = 0)
            {
                EntityRefBuffer.Add(new EntityRefPatchBuffer
                {
                    TypeIndex = TypeManager.GetTypeIndex<TBlobReference>(),
                    BlobEntity = BlobEntity,
                    EntityToPatch = entity,
                    BlobOffset = (byte*) UnsafeUtility.AddressOf(ref blobField) - BlobAddress[blobAssetReferenceIndex],
                    BlobAssetReferenceIndex = blobAssetReferenceIndex
                });
            }
        }
        
        public static unsafe void Bake<TSoClass, TBlobReference, TBlobStruct>(IBaker baker)
            where TSoClass : ScriptableObject, IConvertToBlob<TBlobStruct>
            where TBlobStruct : unmanaged
            where TBlobReference : unmanaged, IComponentData, IBlobAssetReference<TBlobStruct>
        {
            var assetContainerEntity = baker.CreateAdditionalEntity(TransformUsageFlags.None);
            var assetBuffer = baker.AddBuffer<WeakReferenceAssetBuffer>(assetContainerEntity);
            
            var objectRefBuffer = baker.AddBuffer<UnityObjectReferencePatchBuffer>(assetContainerEntity);
            baker.AddComponent<UnityObjectReferencePatchBufferResolved>(assetContainerEntity);
            baker.SetComponentEnabled<UnityObjectReferencePatchBufferResolved>(assetContainerEntity, false);

            var entityRefBuffer = baker.AddBuffer<EntityRefPatchBuffer>(assetContainerEntity);
            baker.AddComponent<EntityRefPatchBufferResolved>(assetContainerEntity);
            baker.SetComponentEnabled<EntityRefPatchBufferResolved>(assetContainerEntity, false);

            var guids = AssetDatabase.FindAssets("t: " + typeof(TSoClass));
            Array.Sort(guids);

            foreach (var guidString in guids)
            {
                var assetGuid = Guid.Parse(guidString);
                var assetPath = AssetDatabase.GUIDToAssetPath(guidString);
                //Debug.Log($"Converting SO: {assetPath} " + typeof(TSOClass));

                var so = AssetDatabase.LoadAssetAtPath<TSoClass>(assetPath);

                if (so == null)
                {
                    Debug.LogError($"Error loading asset from {assetPath}");
                    continue;
                }

                baker.DependsOn(so);

                var blobReferenceEntity = baker.CreateAdditionalEntity(TransformUsageFlags.None, false, so.name + "_Blob");
                
                BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp);
                ref var blob = ref blobBuilder.ConstructRoot<TBlobStruct>();
                
                var context = new Context<TBlobReference>()
                {
                    Baker = baker,
                    AssetBuffer = assetBuffer,
                    ObjectRefBuffer = objectRefBuffer,
                    EntityRefBuffer = entityRefBuffer,
                    BlobEntity = blobReferenceEntity,
                    AssetGuid = assetGuid,
                    BlobAddress = new[] { (byte*) UnsafeUtility.AddressOf(ref blob) }
                };
                
                so.ToBlobData(context, ref blobBuilder, ref blob);
                var blobReference = blobBuilder.CreateBlobAssetReference<TBlobStruct>(Allocator.Persistent);

                baker.AddBlobAsset(ref blobReference, out _);

                var blobReferenceComp = new TBlobReference()
                {
                    guid = Guid.Parse(guidString),
                    blob = blobReference
                };

                baker.AddComponent(blobReferenceEntity, blobReferenceComp);
            }
        }


        public static unsafe void Bake<TSoClass, TBlobReference, TBlobStruct1, TBlobStruct2>(IBaker baker)
            where TSoClass : ScriptableObject, IConvertToBlob<TBlobStruct1, TBlobStruct2>
            where TBlobStruct1 : unmanaged
            where TBlobStruct2 : unmanaged
            where TBlobReference : unmanaged, IComponentData, IBlobAssetReference<TBlobStruct1, TBlobStruct2>
        {
            var entity = baker.GetEntity(TransformUsageFlags.None);
            baker.AddComponent<BakingOnlyEntity>(entity);

            var assetContainerEntity = baker.CreateAdditionalEntity(TransformUsageFlags.None);
            var assetBuffer = baker.AddBuffer<WeakReferenceAssetBuffer>(assetContainerEntity);
            
            var objectRefBuffer = baker.AddBuffer<UnityObjectReferencePatchBuffer>(assetContainerEntity);
            baker.AddComponent<UnityObjectReferencePatchBufferResolved>(assetContainerEntity);
            baker.SetComponentEnabled<UnityObjectReferencePatchBufferResolved>(assetContainerEntity, false);

            var entityRefBuffer = baker.AddBuffer<EntityRefPatchBuffer>(assetContainerEntity);
            baker.AddComponent<EntityRefPatchBufferResolved>(assetContainerEntity);
            baker.SetComponentEnabled<EntityRefPatchBufferResolved>(assetContainerEntity, false);

            var guids = AssetDatabase.FindAssets("t: " + typeof(TSoClass));
            Array.Sort(guids);

            foreach (var guidString in guids)
            {
                var assetGuid = Guid.Parse(guidString);
                var assetPath = AssetDatabase.GUIDToAssetPath(guidString);
                //Debug.Log($"Converting SO: {assetPath} " + typeof(TSOClass));

                var so = AssetDatabase.LoadAssetAtPath<TSoClass>(assetPath);

                if (so == null)
                {
                    Debug.LogError($"Error loading asset from {assetPath}");
                    continue;
                }

                baker.DependsOn(so);

                var blobReferenceEntity = baker.CreateAdditionalEntity(TransformUsageFlags.None, false, so.name + "_Blob");
                
                BlobBuilder blobBuilder1 = new BlobBuilder(Allocator.Temp);
                BlobBuilder blobBuilder2 = new BlobBuilder(Allocator.Temp);

                ref var blob1 = ref blobBuilder1.ConstructRoot<TBlobStruct1>();
                ref var blob2 = ref blobBuilder2.ConstructRoot<TBlobStruct2>();
                
                var context = new Context<TBlobReference>()
                {
                    Baker = baker,
                    AssetBuffer = assetBuffer,
                    ObjectRefBuffer = objectRefBuffer,
                    EntityRefBuffer = entityRefBuffer,
                    BlobEntity = blobReferenceEntity,
                    AssetGuid = assetGuid,
                    BlobAddress = new[] { (byte*) UnsafeUtility.AddressOf(ref blob1), (byte*) UnsafeUtility.AddressOf(ref blob2) }
                };

                so.ToBlobData(context, ref blobBuilder1, ref blob1, ref blob2);

                var blobReference1 = blobBuilder1.CreateBlobAssetReference<TBlobStruct1>(Allocator.Persistent);
                var blobReference2 = blobBuilder2.CreateBlobAssetReference<TBlobStruct2>(Allocator.Persistent);

                baker.AddBlobAsset(ref blobReference1, out _);
                baker.AddBlobAsset(ref blobReference2, out _);

                var blobReferenceComp = new TBlobReference()
                {
                    guid = Guid.Parse(guidString),
                    blob1 = blobReference1,
                    blob2 = blobReference2
                };

                baker.AddComponent(blobReferenceEntity, blobReferenceComp);
            }
        }
    }
}