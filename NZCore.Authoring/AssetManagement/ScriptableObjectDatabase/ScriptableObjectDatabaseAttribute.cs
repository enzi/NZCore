// <copyright project="NZCore.Editor" file="AutoIDManagerAttribute.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.AssetManagement
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class ScriptableObjectDatabaseAttribute : Attribute
    {
        public readonly string ManagerType;
        public readonly string ContainerListProperty;

        public ScriptableObjectDatabaseAttribute(string managerType, string containerListProperty)
        {
            ManagerType = managerType;
            ContainerListProperty = containerListProperty;
        }
    }
}