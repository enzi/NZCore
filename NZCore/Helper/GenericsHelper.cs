using System;

namespace NZCore.Helper
{
    public static class GenericsHelper
    {
        public static bool IsTypeOf<T>(this Type type)
        {
            return typeof (T).IsAssignableFrom(type);
        }
    }
}