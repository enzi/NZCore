// <copyright project="NZCore.Authoring" file="BlobDatabaseVersion.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEngine;

namespace NZCore.Authoring
{
    /// <summary>
    /// Used to bump the version when a change happens which triggers the <see cref="ScriptableObjectDatabaseBaker"/>
    /// </summary>
    [CreateAssetMenu(menuName = "NZCore/BlobDatabaseVersion")]
    public class ScriptableObjectDatabaseVersion : ScriptableObject
    {
        [HideInInspector]
        public int Version;
    }
}