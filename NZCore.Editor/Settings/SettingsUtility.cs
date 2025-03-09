// <copyright project="NZCore.Editor" file="SettingsUtility.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NZCore.Settings
{
    public class SettingsUtility
    {
        private static readonly Dictionary<Type, ISettings> cachedSettings = new();

        public static T GetSettings<T>()
            where T : ScriptableObject, ISettings
        {
            var type = typeof(T);

            if (cachedSettings.TryGetValue(type, out var cached))
            {
                return (T)cached;
            }

            var settings = GetSettings<T>(type);
            cachedSettings.Add(type, settings);
            return settings;
        }

        private static T GetSettings<T>(Type type)
            where T : ScriptableObject, ISettings
        {
            var filter = type.Namespace == null ? type.Name : $"{type.Namespace}.{type.Name}";
            var assets = AssetDatabase.FindAssets($"t:{filter}");
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(assets.First()));
        }
    }
}