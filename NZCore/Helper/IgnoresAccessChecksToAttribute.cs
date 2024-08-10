// <copyright project="NZCore" file="IgnoresAccessChecksToAttribute.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
            AssemblyName = assemblyName;
        }

        public string AssemblyName { get; }
    }
}