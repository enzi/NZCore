// <copyright project="NZCore" file="PackagePathAttribute.cs" version="0.1">
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