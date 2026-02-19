// <copyright project="NZCore.Authoring" file="BlobDatabase.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.AssetManagement;
using Unity.Entities;
using UnityEngine;

namespace NZCore.Authoring
{
    public class ScriptableObjectDatabaseAuthoring : MonoBehaviour
    {
        public ScriptableObjectDatabaseVersion Version;

        private class ScriptableObjectDatabaseBaker : Baker<ScriptableObjectDatabaseAuthoring>
        {
            public override void Bake(ScriptableObjectDatabaseAuthoring authoring)
            {
                if (authoring.Version != null)
                {
                    DependsOn(authoring.Version);
                }
                
                foreach (var converter in ScriptableObjectDatabaseCollector.SettingConverters)
                {
                    var instance = ScriptableObject.CreateInstance(converter);

                    if (instance is ISettingsDatabase settingsDatabase)
                    {
                        var entity = CreateAdditionalEntity(TransformUsageFlags.None, false, converter.Name);
                        settingsDatabase.BakeDatabase(this, entity);
                    }

                    //DestroyImmediate(instance);
                }

                foreach (var converter in ScriptableObjectDatabaseCollector.BlobConverters)
                {
                    var instance = ScriptableObject.CreateInstance(converter);

                    if (instance is IConvertToBlob blobBaker)
                    {
                        blobBaker.Bake(this);
                    }
                }
            }
        }
    }
}