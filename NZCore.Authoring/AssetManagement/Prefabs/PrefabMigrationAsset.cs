// <copyright project="NZCore.Authoring" file="PrefabMigrationAsset.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NZCore.AssetManagement
{
    [CreateAssetMenu(fileName = "PrefabMigrations", menuName = "NZCore/Create Prefab Migrations")]
    public class PrefabMigrationAsset : ScriptableObject
    {
        public List<PrefabMigrationEntry> Migrations = new();

        public static List<PrefabMigrationAsset> FindAll() =>
            AssetDatabase.FindAssets($"t:{typeof(PrefabMigrationAsset).FullName}")
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Distinct()
                         .Select(AssetDatabase.LoadAssetAtPath<PrefabMigrationAsset>)
                         .Where(asset => asset != null)
                         .ToList();
    }

    [Serializable]
    public struct PrefabMigrationEntry
    {
        public PrefabId From;
        public PrefabId To;
    }
}