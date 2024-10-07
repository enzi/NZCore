// <copyright project="NZCore.Editor" file="CscPathAttribute.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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