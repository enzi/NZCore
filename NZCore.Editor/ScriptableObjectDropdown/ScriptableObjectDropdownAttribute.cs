// <copyright project="NZCore.Editor" file="ScriptableObjectDropdownAttribute.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
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