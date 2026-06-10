// <copyright project="NZCore.Editor" file="ReflectionUtility.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Reflection;

namespace NZCore.Editor
{
    public static class ReflectionUtility
    {
        public static TProperty GetReflectedProperty<TProvider, TProperty>(TProvider provider, string propertyName)
        {
            var searchProviderType = typeof(TProvider);
            var tableConfigProperty = searchProviderType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (tableConfigProperty != null)
            {
                return (TProperty)tableConfigProperty.GetValue(provider);
            }

            throw new MissingMemberException($"{propertyName} property not found in provider class");
        }

        public static void SetReflectedProperty<TProvider, TProperty>(TProvider provider, string propertyName, TProperty value)
        {
            var providerType = typeof(TProvider);
            var tableConfigProperty = providerType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (tableConfigProperty == null)
            {
                throw new MissingMemberException($"{propertyName} property not found in provider class");
            }

            tableConfigProperty.SetValue(provider, value);
        }
        
        /// <summary>
        /// Returns the higher type of a base T
        /// </summary>
        public static Type GetRootType<T>(Type type)
            where T : class
        {
            var current = type;
            while (current.BaseType != null && current.BaseType != typeof(T))
            {
                current = current.BaseType;
            }

            return current;
        }
    }
}