// <copyright project="NZCore" file="AutoIDManagerAttribute.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System;

namespace NZCore.AssetManagement
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class AutoIDManagerAttribute : Attribute
    {
        public readonly string ManagerType;
        public readonly string ContainerListProperty;

        public AutoIDManagerAttribute(string managerType, string containerListProperty)
        {
            ManagerType = managerType;
            ContainerListProperty = containerListProperty;
        }
    }
}