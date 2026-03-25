// <copyright project="NZCore.Editor" file="Serializabletype.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using UnityEngine;

namespace NZCore.Editor
{
    [Serializable]
    public class SerializableType
    {
        [SerializeField] private string assemblyQualifiedName;

        public Type Type
        {
            get => string.IsNullOrEmpty(assemblyQualifiedName) ? null : Type.GetType(assemblyQualifiedName);
            set => assemblyQualifiedName = value?.AssemblyQualifiedName ?? "";
        }
    }
}