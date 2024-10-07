// <copyright project="NZCore.Editor" file="ScriptableObjectDropdownAttribute.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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