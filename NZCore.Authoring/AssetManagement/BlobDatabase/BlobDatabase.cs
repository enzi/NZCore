// <copyright project="NZCore.Authoring" file="BlobDatabase.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.AssetManagement;
using Unity.Entities;
using UnityEngine;

namespace NZCore.Authoring
{
    public class BlobDatabase : MonoBehaviour
    {
        private class BlobDatabaseBaker : Baker<BlobDatabase>
        {
            public override void Bake(BlobDatabase authoring)
            {
                foreach (var converter in BlobDatabaseCollector.Converters)
                {
                    var instance = Activator.CreateInstance(converter);
                
                    if (instance is IConvertToBlob blobConverter)
                    {
                        blobConverter.Bake(this);
                    }
                }
            }
        }
    }
}