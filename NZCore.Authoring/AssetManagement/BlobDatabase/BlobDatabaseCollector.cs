// <copyright project="NZCore.Editor" file="BlobDatabase.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using UnityEditor;

namespace NZCore.AssetManagement
{
    public static class BlobDatabaseCollector
    {
        public static Type[] Converters;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            var converters = TypeCache.GetTypesDerivedFrom<IConvertToBlob>();

            var list = new List<Type>();
            foreach (var converter in converters)
            {
                if (converter.IsInterface || converter.IsAbstract || converter.ContainsGenericParameters)
                {
                    continue;
                }

                list.Add(converter);
            }

            Converters = list.ToArray();
        }
    }
}