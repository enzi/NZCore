// <copyright project="NZCore" file="GenericsHelper.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.Helper
{
    public static class GenericsHelper
    {
        public static bool IsTypeOf<T>(this Type type)
        {
            return typeof(T).IsAssignableFrom(type);
        }
    }
}