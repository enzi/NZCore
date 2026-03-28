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
}