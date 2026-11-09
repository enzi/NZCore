// <copyright project="NZCore.Testing" file="TestAssets.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NZCore
{
    public static class TestAssets
    {
        public static string CreateFolder(string name)
        {
            AssetDatabase.CreateFolder("Assets", name);
            return $"Assets/{name}";
        }

        public static T Create<T>(string folder, string name, Action<T> setup = null)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            setup?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, $"{folder}/{name}.asset");
            return asset;
        }

        public static GameObject CreatePrefab(string folder, GameObject root)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{root.name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static GameObject CreateVariant(string folder, GameObject basePrefab, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.name = name;
            var variant = PrefabUtility.SaveAsPrefabAsset(instance, $"{folder}/{name}.prefab");
            Object.DestroyImmediate(instance);
            return variant;
        }

        public static void Save(Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}