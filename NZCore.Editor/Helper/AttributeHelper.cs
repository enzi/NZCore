using System;
using System.Reflection;

namespace NZCore.Editor
{
    public static class AttributeHelper
    {
        public static T GetCustomAttributeRecursive<T>(this Type type, out Type baseType) where T : Attribute
        {
            while (true)
            {
                var attribute = type.GetCustomAttribute<T>();

                if (attribute != null)
                {
                    baseType = type;
                    return attribute;
                }

                if (type.BaseType == null)
                {
                    baseType = null;
                    return null;
                }

                type = type.BaseType;
            }
        }
    }
}