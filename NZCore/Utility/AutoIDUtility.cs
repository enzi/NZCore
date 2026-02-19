// <copyright project="NZCore" file="AutoIDUtility.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using NZCore.AssetManagement;
using UnityEngine;

namespace NZCore
{
    public static class AutoIDUtility
    {
        public static int GetHighestIndex<T>(this List<T> list)
            where T : ScriptableObjectWithAutoID
        {
            int highestIndex = 0;
            for (var i = 0; i < list.Count; i++)
            {
                var element = list[i];
                if (element == null)
                {
                    Debug.LogError($"Error in list {typeof(T).Name}! Element is null at index {i}.");
                    continue;
                }

                if (element.AutoID > highestIndex)
                    highestIndex = element.AutoID;
            }

            return highestIndex;
        }
    }
}