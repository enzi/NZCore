// <copyright project="NZCore.Editor" file="NZPropertyFieldAttribute.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using UnityEngine;

namespace NZCore.Editor
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class NZPropertyFieldAttribute : PropertyAttribute
    {
        public bool Inline;

        public NZPropertyFieldAttribute(bool inline)
        {
            Inline = inline;
        }
    }
}