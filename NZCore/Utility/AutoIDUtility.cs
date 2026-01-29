// <copyright project="NZCore" file="AutoIDUtility.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using NZCore.AssetManagement;

namespace NZCore
{
    public static class AutoIDUtility
    {
        public static int GetHighestIndex<T>(this List<T> container)
            where T : ScriptableObjectWithAutoID
        {
            int highestIndex = 0;
            foreach (var element in container)
            {
                if (element.AutoID > highestIndex)
                    highestIndex = element.AutoID;
            }

            return highestIndex;
        }
    }
}