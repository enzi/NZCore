// <copyright project="NZCore.Editor" file="GenericBlobGroupBaker.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using NZCore.AssetManagement;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace NZCore
{
    public interface IConvertToBlobGroup<TSoClass, TGroupReference, TBlobStruct> : IConvertToBlob<TBlobStruct>
        where TSoClass : ScriptableObjectWithAutoID, IConvertToBlob<TBlobStruct>
        where TBlobStruct : unmanaged
        where TGroupReference : unmanaged, IComponentData, IBlobAssetReference<BlobGroup<TBlobStruct>>
    {
        void IConvertToBlob.Bake(IBaker baker)
        {
            GenericBlobGroupBaker.Bake<TSoClass, TGroupReference, TBlobStruct>(baker);
        }
    }

    public static class GenericBlobGroupBaker
    {
        private class GroupContext : GenericBlobBaker.ContextBase
        {
            public override void AddObjectRef<T>(T asset, ref UnityObjectRefForBlob<T> blobField, int blobAssetReferenceIndex = 0)
            {
                throw new NotSupportedException("UnityObjectRef patching is not supported by GenericBlobGroupBaker yet. Use the per-item IConvertToBlob path.");
            }

            public override void AddEntityRef(Entity entity, ref Entity blobField, int blobAssetReferenceIndex = 0)
            {
                throw new NotSupportedException("Entity patching is not supported by GenericBlobGroupBaker yet. Use the per-item IConvertToBlob path.");
            }
        }

        public static void Bake<TSoClass, TGroupReference, TBlobStruct>(IBaker baker)
            where TSoClass : ScriptableObjectWithAutoID, IConvertToBlob<TBlobStruct>
            where TBlobStruct : unmanaged
            where TGroupReference : unmanaged, IComponentData, IBlobAssetReference<BlobGroup<TBlobStruct>>
        {
            var guids = AssetDatabase.FindAssets("t: " + typeof(TSoClass));
            Array.Sort(guids);

            var assets = new List<TSoClass>(guids.Length);
           
            foreach (var guidString in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guidString);

                var so = AssetDatabase.LoadAssetAtPath<TSoClass>(assetPath);

                if (so == null)
                {
                    Debug.LogError($"Error loading asset from {assetPath}");
                    continue;
                }

                baker.DependsOn(so);
                assets.Add(so);
            }

            var count = assets.GetHighestIndex() + 1;

            var groupEntity = baker.CreateAdditionalEntity(TransformUsageFlags.None, false, typeof(TSoClass).Name + "_BlobGroup");

            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var root = ref blobBuilder.ConstructRoot<BlobGroup<TBlobStruct>>();
            var items = blobBuilder.Allocate(ref root.Items, count);

            // BlobBuilder memory is not guaranteed zeroed, gap slots need explicit defaults
            for (var i = 0; i < items.Length; i++)
            {
                items[i] = default;
            }

            var context = new GroupContext
            {
                Baker = baker,
                BlobEntity = groupEntity
            };

            foreach (var so in assets)
            {
                try
                {
                    so.ToBlobData(context, ref blobBuilder, ref items[so.AutoID]);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{e.Message}\n{e.StackTrace}", so);
                }
            }

            var blobReference = blobBuilder.CreateBlobAssetReference<BlobGroup<TBlobStruct>>(Allocator.Persistent);

            baker.AddBlobAsset(ref blobReference, out _);
            baker.AddComponent(groupEntity, new TGroupReference
            {
                blob = blobReference
            });
        }
    }
}
