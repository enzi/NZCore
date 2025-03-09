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
            Type searchProviderType = typeof(TProvider);
            PropertyInfo tableConfigProperty = searchProviderType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (tableConfigProperty != null)
            {
                return (TProperty) tableConfigProperty.GetValue(provider);
            }

            throw new MissingMemberException($"{propertyName} property not found in provider class");
        }
        
        public static void SetReflectedProperty<TProvider, TProperty>(TProvider provider, string propertyName, TProperty value)
        {
            Type providerType = typeof(TProvider);
            PropertyInfo tableConfigProperty = providerType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (tableConfigProperty == null)
            {
                throw new MissingMemberException($"{propertyName} property not found in provider class");
            }

            tableConfigProperty.SetValue(provider, value);
        }
    }
}