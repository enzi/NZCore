// <copyright project="NZCore.Editor" file="AutoIDManagerAttribute.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.AssetManagement
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class RegisterInScriptableObjectDatabaseAttribute : Attribute
    {
        public readonly string ManagerType;
        public readonly string ContainerListProperty;
        public readonly Type GroupByType;

        public RegisterInScriptableObjectDatabaseAttribute(string managerType, string containerListProperty, Type groupByType = null)
        {
            ManagerType = managerType;
            ContainerListProperty = containerListProperty;
            GroupByType = groupByType;
        }
    }
}