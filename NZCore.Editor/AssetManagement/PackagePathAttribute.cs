// <copyright project="NZCore.Editor" file="PackagePathAttribute.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.Editor.AssetManagement
{
    public class PackagePathAttribute : Attribute
    {
        public readonly string Path;
        public bool AddUniqueSettingsPath;

        public PackagePathAttribute(string path, bool addUniqueSettingsPath)
        {
            Path = path;
            AddUniqueSettingsPath = addUniqueSettingsPath;
        }
    }
}