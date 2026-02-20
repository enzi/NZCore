// <copyright project="NZCore.Editor" file="SettingsUtility.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using NZCore.AssetManagement;
using UnityEditor;
using UnityEngine;

namespace NZCore.Settings
{
    public static class SettingsUtility
    {
        private static readonly Dictionary<Type, ISettingsBaker> CachedSettings = new();

        public static T GetSettings<T>()
            where T : ScriptableObject, ISettingsBaker
        {
            var type = typeof(T);

            if (CachedSettings.TryGetValue(type, out var cached) && (T) cached != null)
            {
                return (T)cached;
            }

            var settings = GetSettings<T>(type);
            CachedSettings[type] = settings;
            return settings;
        }

        private static T GetSettings<T>(Type type)
            where T : ScriptableObject, ISettingsBaker
        {
            var filter = type.Namespace == null ? type.Name : $"{type.Namespace}.{type.Name}";
            var assets = AssetDatabase.FindAssets($"t:{filter}");
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(assets.First()));
        }
    }
}