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

    public abstract class ScriptableObjectDatabase<T> : ScriptableObject, ISettingsBaker, ISettingsDatabase
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
        public static void Rebuild()
        {
        }

        public static void DeleteAsset(Object assetToBeDeleted)
        {
            var type = assetToBeDeleted.GetType();

            if (!TryGet(type, out var manager, out var managerObject, out var list))
                return;

            bool hasDeletion = false;
            for (int i = list.arraySize - 1; i >= 0; i--)
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
            if (!TryGet(type, out var manager, out var managerObject, out var list))
                return;

            // Cleanup null entries first
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    list.DeleteArrayElementAtIndex(i);
                }
            }

            var currentObjects = new List<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var obj = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (obj != null)
                {
                    currentObjects.Add(obj);
                }
            }

            var foundObjects = AssetDatabase.FindAssets($"t:{type.Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .Where(s => s.GetType() == type)
                .ToList();

            var currentSet = new HashSet<Object>(currentObjects);
            var foundSet = new HashSet<Object>(foundObjects);

            if (currentSet.SetEquals(foundSet))
                return;

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

        private static bool TryGet(Type type, out ScriptableObject manager, out SerializedObject managerObject, out SerializedProperty containerListProperty)
        {
            manager = null;
            managerObject = null;
            containerListProperty = null;

            var attribute = type.GetCustomAttributeRecursive<RegisterInScriptableObjectDatabaseAttribute>(out _);
            if (attribute == null)
                return false;

            var managerGuid = AssetDatabase.FindAssets($"t:{attribute.ManagerType}");

            if (managerGuid.Length == 0)
            {
                Debug.LogError($"No manager found for {attribute.ManagerType}");
                return false;
            }

            if (managerGuid.Length > 1)
            {
                Debug.LogError($"More than one manager found for {attribute.ManagerType}");
                return false;
            }

            manager = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(managerGuid[0]));
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
    }
}