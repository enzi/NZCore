// <copyright project="NZCore.Editor" file="CscPathAttribute.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.Editor.AssetManagement
{
    public class CscPathAttribute : Attribute
    {
        public string[] Path;

        public CscPathAttribute(params string[] path)
        {
            Path = path;
        }
    }
}