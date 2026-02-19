// <copyright project="NZCore.Editor" file="BlobDatabase.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using UnityEditor;

namespace NZCore.AssetManagement
{
    public static class ScriptableObjectDatabaseCollector
    {
        public static Type[] BlobConverters;
        public static Type[] SettingConverters;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            BlobConverters = GetFromInterface<IConvertToBlob>();
            SettingConverters = GetFromInterface<ISettingsDatabase>();
        }

        private static Type[] GetFromInterface<T>()
        {
            var converters = TypeCache.GetTypesDerivedFrom<T>();

            var list = new List<Type>();
            foreach (var converter in converters)
            {
                if (converter.IsInterface || converter.IsAbstract || converter.ContainsGenericParameters)
                {
                    continue;
                }

                list.Add(converter);
            }

            return list.ToArray();
        }
    }
}