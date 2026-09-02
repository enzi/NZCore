// <copyright project="NZCore.Editor" file="SourceGeneratorAttribute.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.Editor.AssetManagement
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class SourceGeneratorAttribute : Attribute
    {
        public readonly string AssemblyName;

        public SourceGeneratorAttribute(string assemblyName)
        {
            AssemblyName = assemblyName;
        }
    }
}
