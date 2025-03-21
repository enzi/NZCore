// <copyright project="NZCore.Editor" file="AttributeUtility.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NZCore.Editor
{
    public static class AttributeUtility
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

        public static List<TypeWithAttribute<T>> FindAttributes<T>()
            where T : Attribute
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            List<TypeWithAttribute<T>> list = new List<TypeWithAttribute<T>>();
            
            foreach (var assembly in assemblies)
            {
                Type[] types = assembly.GetTypes();
                
                var matchingTypes = types.Where(type => Attribute.IsDefined(type, typeof(T)));

                foreach (var type in matchingTypes)
                {
                    list.Add(new TypeWithAttribute<T>()
                    {
                        Type = type,
                        Attribute = type.GetCustomAttribute<T>()
                    });
                }
            }

            return list;
        }

        public class TypeWithAttribute<T>
            where T : Attribute
        {
            public Type Type;
            public T Attribute;
        }
    }
}