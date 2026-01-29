// <copyright project="NZCore.Editor" file="ScriptableObjectDatabase.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using NZCore.Editor;
using NZCore.Settings;
using UnityEditor;
using UnityEngine;

namespace NZCore.AssetManagement
{
    public interface IIndexableDatabase
    {
        public void CreateLookup();
    }
    
    public abstract class ScriptableObjectDatabase<T> : SettingsBase
        where T : ScriptableObject
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
                        Debug.LogError($"Request DB {typeof(T).Name} could not be found!");
                    }
                }

                return instance;
            }
        }
    }
    
    public static class ScriptableObjectDatabase
    {
        [MenuItem("Tools/Rebuild SO DB")]
        public static void Rebuild()
        {
        }
        
        private static bool TryGet(Type type, out ScriptableObject manager, out SerializedObject managerObject, out SerializedProperty containerListProperty)
        {
            manager = null;
            managerObject = null;
            containerListProperty = null;

            var attribute = type.GetCustomAttributeRecursive<ScriptableObjectDatabaseAttribute>(out _);
            if (attribute == null)
            {
                return false;
            }

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

        private static void Clear(Type type)
        {
            if (!TryGet(type, out var manager, out var managerObject, out var list))
                return;

            list.ClearArray();
            managerObject.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(manager);
        }

        public static void Update(Type type)
        {
            if (!TryGet(type, out var manager, out var managerObject, out var list))
                return;

            var currentObjects = new List<UnityEngine.Object>();
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

            bool hasChanges = false;

            if (currentObjects.Count != foundObjects.Count)
            {
                hasChanges = true;
            }
            else
            {
                var currentSet = new HashSet<UnityEngine.Object>(currentObjects);
                var foundSet = new HashSet<UnityEngine.Object>(foundObjects);

                if (!currentSet.Equals((foundSet)))
                {
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                list.ClearArray();

                foreach (var obj in foundObjects)
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = obj;
                }
                
                managerObject.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(manager);

                if (manager is IIndexableDatabase indexableDatabase)
                {
                    indexableDatabase.CreateLookup();
                }
            }
        }
    }
}