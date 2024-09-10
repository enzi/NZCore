// <copyright project="NZCore" file="ScriptableObjectConverter_Baker.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace NZCore
{
    public abstract class ScriptableObjectConverter_Baker<TAuthoringType, TSOClass, TBlobStruct, TBlobReference> : Baker<TAuthoringType>
        where TAuthoringType : ScriptableObjectConverterBase<TSOClass>
        where TSOClass : ScriptableObject, IConvertToBlob<TBlobStruct>
        where TBlobStruct : unmanaged
        where TBlobReference : unmanaged, IComponentData, IBlobAssetReference<TBlobStruct>
    {
        public override void Bake(TAuthoringType converter)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<BakingOnlyEntity>(entity);

            //Debug.Log($"Running ScriptableObjectConverter for {converter.name}");
            converter.GatherScriptableObjects();

            foreach (var guidString in converter.ScriptableObjects)
            {
                var guid = Guid.Parse(guidString);
                var assetPath = AssetDatabase.GUIDToAssetPath(guidString);
                //Debug.Log($"Converting SO: {assetPath} " + typeof(TSOClass));

                var so = AssetDatabase.LoadAssetAtPath<TSOClass>(assetPath);

                if (so == null)
                {
                    Debug.LogError($"Error loading asset from {assetPath}");
                    continue;
                }

                DependsOn(so);

                var blobReferenceEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, so.name + "_Blob");

                BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp);

                ref var blob = ref blobBuilder.ConstructRoot<TBlobStruct>();
                so.ToBlobData(this, ref blobBuilder, ref blob, blobReferenceEntity, guid);
                var blobReference = blobBuilder.CreateBlobAssetReference<TBlobStruct>(Allocator.Persistent);

                AddBlobAsset(ref blobReference, out _);

                var blobReferenceComp = new TBlobReference()
                {
                    guid = Guid.Parse(guidString),
                    blob = blobReference
                };

                AddComponent(blobReferenceEntity, blobReferenceComp);
            }
        }
    }

    public abstract class ScriptableObjectConverter_Baker<TAuthoringType, TSOClass, TBlobStruct1, TBlobStruct2, TBlobReference> : Baker<TAuthoringType>
        where TAuthoringType : ScriptableObjectConverterBase<TSOClass>
        where TSOClass : ScriptableObject, IConvertToBlob<TBlobStruct1, TBlobStruct2>
        where TBlobStruct1 : unmanaged
        where TBlobStruct2 : unmanaged
        where TBlobReference : unmanaged, IComponentData, IBlobAssetReference<TBlobStruct1, TBlobStruct2>
    {
        public override void Bake(TAuthoringType converter)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<BakingOnlyEntity>(entity);

            //Debug.Log($"Running ScriptableObjectConverter for {converter.name}");    
            converter.GatherScriptableObjects();

            foreach (var guidString in converter.ScriptableObjects)
            {
                var guid = Guid.Parse(guidString);
                var assetPath = AssetDatabase.GUIDToAssetPath(guidString);
                //Debug.Log($"Converting SO: {assetPath} " + typeof(TSOClass));

                var so = AssetDatabase.LoadAssetAtPath<TSOClass>(assetPath);

                if (so == null)
                {
                    Debug.LogError($"Error loading asset from {assetPath}");
                    continue;
                }

                DependsOn(so);

                var blobReferenceEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, so.name + "_Blob");

                BlobBuilder blobBuilder1 = new BlobBuilder(Allocator.Temp);
                BlobBuilder blobBuilder2 = new BlobBuilder(Allocator.Temp);

                ref var blob1 = ref blobBuilder1.ConstructRoot<TBlobStruct1>();
                ref var blob2 = ref blobBuilder2.ConstructRoot<TBlobStruct2>();

                so.ToBlobData(this, ref blobBuilder1, ref blob1, ref blob2, blobReferenceEntity, guid);

                var blobReference1 = blobBuilder1.CreateBlobAssetReference<TBlobStruct1>(Allocator.Persistent);
                var blobReference2 = blobBuilder2.CreateBlobAssetReference<TBlobStruct2>(Allocator.Persistent);

                AddBlobAsset(ref blobReference1, out _);
                AddBlobAsset(ref blobReference2, out _);

                var blobReferenceComp = new TBlobReference()
                {
                    guid = Guid.Parse(guidString),
                    blob1 = blobReference1,
                    blob2 = blobReference2
                };

                AddComponent(blobReferenceEntity, blobReferenceComp);
            }
        }
    }
}