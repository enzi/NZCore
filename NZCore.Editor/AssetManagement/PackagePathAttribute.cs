// <copyright project="NZCore.Editor" file="PackagePathAttribute.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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