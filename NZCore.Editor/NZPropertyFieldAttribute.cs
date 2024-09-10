// <copyright project="NZCore" file="NZPropertyFieldAttribute.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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