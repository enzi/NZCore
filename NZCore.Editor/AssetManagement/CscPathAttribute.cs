// <copyright project="NZCore" file="CscPathAttribute.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
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