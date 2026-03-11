// <copyright project="NZCore" file="GenericsHelper.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.Helper
{
    public static class GenericsHelper
    {
        public static bool IsTypeOf<T>(this Type type) => typeof(T).IsAssignableFrom(type);

        public static bool HasBaseType(this Type type, Type requiredType) =>
            type.BaseType == requiredType || (type.BaseType != null && type.BaseType.IsSubclassOf(requiredType));
    }
}