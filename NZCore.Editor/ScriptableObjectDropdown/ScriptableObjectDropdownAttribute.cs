// <copyright project="NZCore" file="ScriptableObjectDropdownAttribute.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System;
using UnityEngine;

namespace NZCore.Editor
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ScriptableObjectDropdownAttribute : PropertyAttribute
    {
        public bool UseFlags;

        public ScriptableObjectDropdownAttribute(bool useFlags = false)
        {
            UseFlags = useFlags;
        }
    }
}