// <copyright project="NZCore.Editor" file="Serializabletype.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace NZCore.Editor
{
    [Serializable]
    public class SerializableType
    {
        [FormerlySerializedAs("assemblyQualifiedName")] [SerializeField] 
        private string _assemblyQualifiedName;

        public Type Type
        {
            get => string.IsNullOrEmpty(_assemblyQualifiedName) ? null : Type.GetType(_assemblyQualifiedName);
            set => _assemblyQualifiedName = value?.AssemblyQualifiedName ?? "";
        }
    }

    /// <summary>
    /// Limits the type picker of a SerializableType field to types deriving from or implementing BaseType.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializableTypeFilterAttribute : Attribute
    {
        public Type BaseType { get; }

        public SerializableTypeFilterAttribute(Type baseType)
        {
            BaseType = baseType;
        }
    }
}