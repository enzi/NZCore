// <copyright project="NZCore" file="AutoIDUtility.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System.Collections.Generic;
using NZCore.AssetManagement;

namespace NZCore.Utility
{
    public static class AutoIDUtility
    {
        public static int GetHighestIndex<T>(this List<T> container)
            where T : ScriptableObjectWithAutoID
        {
            int highestIndex = 0;
            foreach (var dynamicStat in container)
            {
                if (dynamicStat.AutoID > highestIndex)
                    highestIndex = dynamicStat.AutoID;
            }

            return highestIndex;
        }

        // public static int GetHighestIndex<T>(this List<T> container)
        //     where T : ScriptableObjectWithDefaultAutoID
        // {
        //     int highestIndex = 0;
        //     foreach (var dynamicStat in container)
        //     {
        //         if (dynamicStat.AutoID > highestIndex)
        //             highestIndex = dynamicStat.AutoID;
        //     }
        //
        //     return highestIndex;
        // }
    }
}