// <copyright project="NZCore.Editor" file="PackagePathAttribute.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.Editor.AssetManagement
{
    public class PackagePathAttribute : Attribute
    {
        public string Path;

        public PackagePathAttribute(string path)
        {
            Path = path;
        }
    }
}