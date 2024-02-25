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