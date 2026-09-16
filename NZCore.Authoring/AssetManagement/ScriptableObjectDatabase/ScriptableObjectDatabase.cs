// <copyright project="NZCore.Editor" file="ScriptableObjectDatabase.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using NZCore.Editor;
using NZCore.Settings;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NZCore.AssetManagement
{
    public interface IIndexableDatabase
    {
        public void CreateLookup();
    }

    public interface ISettingsBaker
    {
        public void Bake(IBaker baker, Entity entity);
    }

    public interface ISettingsDatabase
    {
        public void BakeDatabase(IBaker baker, Entity entity);
    }

#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    public abstract partial class ScriptableObjectDatabase<T> : ScriptableObject, ISettingsBaker, ISettingsDatabase
        where T : ScriptableObject, ISettingsBaker
    {
        private static T instance;

        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    var assets = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

                    if (assets.Length > 0)
                    {
                        instance = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(assets[0]));

                        if (instance is IIndexableDatabase indexableDatabase)
                        {
                            indexableDatabase.CreateLookup();
                        }
                    }
                    else
                    {
                        Debug.LogError($"Requested {typeof(T).Name} ScriptableObjectDatabase could not be found!");
                    }
                }

                return instance;
            }
        }

        public abstract void Bake(IBaker baker, Entity entity);

        public void BakeDatabase(IBaker baker, Entity entity)
        {
            var settings = SettingsUtility.GetSettings<T>();
            baker.DependsOn(settings);
            settings.Bake(baker, entity);
        }
    }

    public static class ScriptableObjectDatabase
    {
        [MenuItem("Tools/Rebuild SO DB")]
        public static void Rebuild() { }

        public static void DeleteAsset(Object assetToBeDeleted)
        {
            if (!TryGet(assetToBeDeleted.GetType(), out var manager, out var managerObject, out var list, out _))
            {
                return;
            }

            var hasDeletion = false;
            for (var i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == assetToBeDeleted)
                {
                    hasDeletion = true;
                    list.DeleteArrayElementAtIndex(i);
                }
            }

            if (hasDeletion)
            {
                managerObject.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(manager);
            }
        }

        public static void Update(Type type)
        {
            if (!TryGet(type, out var manager, out var managerObject, out var list, out var groupByType))
            {
                return;
            }

            // Cleanup null entries first
            for (var i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    list.DeleteArrayElementAtIndex(i);
                }
            }

            var currentObjects = new List<Object>();
            for (var i = 0; i < list.arraySize; i++)
            {
                var obj = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (obj != null)
                {
                    currentObjects.Add(obj);
                }
            }

            var assetType = groupByType != null ? groupByType : type;

            var foundObjects = AssetDatabase.FindAssets($"t:{assetType.Name}")
                                            .Select(AssetDatabase.GUIDToAssetPath)
                                            .Distinct()
                                            .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                                            .Where(s => s != null && assetType.IsAssignableFrom(s.GetType()))
                                            .ToList();

            var currentSet = new HashSet<Object>(currentObjects);
            var foundSet = new HashSet<Object>(foundObjects);

            if (currentSet.SetEquals(foundSet))
            {
                return;
            }

            list.ClearArray();

            foreach (var obj in foundObjects)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = obj;
            }

            managerObject.ApplyModifiedPropertiesWithoutUndo();

            if (manager is IIndexableDatabase indexableDatabase)
            {
                try
                {
                    indexableDatabase.CreateLookup();
                }
                catch (Exception e)
                {
                    Debug.LogError($"{e.Message}\n{e.StackTrace}");
                }
            }

            AssetDatabase.SaveAssetIfDirty(manager);
        }

        private static bool TryGet(Type type, 
            out ScriptableObject manager, out SerializedObject managerObject, 
            out SerializedProperty containerListProperty, out Type groupByType)
        {
            manager = null;
            managerObject = null;
            containerListProperty = null;
            groupByType = null;

            var attribute = type.GetCustomAttributeRecursive<RegisterInScriptableObjectDatabaseAttribute>(out _);
            if (attribute == null)
            {
                return false;
            }

            groupByType = attribute.GroupByType;

            var managerGuid = AssetDatabase.FindAssets($"t:{attribute.ManagerType}");

            if (managerGuid.Length > 1)
            {
                Debug.LogError($"More than one manager found for {attribute.ManagerType}");
                return false;
            }

            if (managerGuid.Length == 0)
            {
                if (!TryCreateManager(attribute.ManagerType, out manager))
                {
                    return false;
                }
            }
            else
            {
                manager = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(managerGuid[0]));
            }

            if (manager == null)
            {
                Debug.LogError("Manager wasn't a ScriptableObject");
                return false;
            }

            managerObject = new SerializedObject(manager);
            containerListProperty = managerObject.FindProperty(attribute.ContainerListProperty);
            if (containerListProperty == null)
            {
                Debug.LogError($"Property {attribute.ContainerListProperty} not found for {attribute.ManagerType}");
                return false;
            }

            if (!containerListProperty.isArray)
            {
                Debug.LogError($"Property {attribute.ContainerListProperty} was not type of array for {attribute.ManagerType}");
                return false;
            }

            return true;
        }

        private static bool TryCreateManager(string managerTypeName, out ScriptableObject manager)
        {
            manager = null;
            var managerTypes = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                                        .Where(type => !type.IsAbstract
                                                       && typeof(ISettingsDatabase).IsAssignableFrom(type)
                                                       && (type.Name == managerTypeName || type.FullName == managerTypeName))
                                        .ToList();

            if (managerTypes.Count != 1)
            {
                Debug.LogError(managerTypes.Count == 0
                    ? $"Manager type {managerTypeName} was not found"
                    : $"More than one manager type found for {managerTypeName}");
                return false;
            }

            var settingsRoot = SettingsOverviewWindow.SettingsRoot?.TrimEnd('/');
            if (!EnsureFolder(settingsRoot))
            {
                return false;
            }

            var managerType = managerTypes[0];
            var path = AssetDatabase.GenerateUniqueAssetPath($"{settingsRoot}/{managerType.Name}.asset");
            manager = ScriptableObject.CreateInstance(managerType);
            AssetDatabase.CreateAsset(manager, path);
            Debug.Log($"Created {managerType.Name} at {path}", manager);
            return true;
        }

        private static bool EnsureFolder(string path)
        {
            if (path == "Assets")
            {
                return true;
            }

            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                Debug.LogError($"Settings folder must be inside Assets: {path}");
                return false;
            }

            var parent = "Assets";
            foreach (var folder in path.Substring("Assets/".Length).Split('/'))
            {
                var current = $"{parent}/{folder}";
                if (!AssetDatabase.IsValidFolder(current) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, folder)))
                {
                    Debug.LogError($"Could not create settings folder: {current}");
                    return false;
                }

                parent = current;
            }

            return true;
        }
    }
}
