// <copyright project="NZCore.Tests.Authoring" file="PrefabDatabaseTestAuthoring.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Linq;
using NZCore.AssetManagement;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace NZCore.Tests.Authoring
{
    public class PrefabDatabaseTestAuthoring : MonoBehaviour
    {
        public string Folder;

        private class Baker : Baker<PrefabDatabaseTestAuthoring>
        {
            public override void Bake(PrefabDatabaseTestAuthoring authoring)
            {
                var folder = $"{authoring.Folder}/";

                PrefabDatabase.Bake(this, GetEntity(TransformUsageFlags.None),
                    PrefabDefinition.FindAll().Where(definition => AssetDatabase.GetAssetPath(definition).StartsWith(folder)).ToList(),
                    PrefabMigrationAsset.FindAll().Where(asset => AssetDatabase.GetAssetPath(asset).StartsWith(folder)).ToList());
            }
        }
    }
}