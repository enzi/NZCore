// <copyright project="NZCore.Authoring" file="SettingsBase.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Entities;
using UnityEngine;

namespace NZCore.Settings
{
    [Serializable]
    public abstract class SettingsBase : ScriptableObject, ISettings
    {
        public abstract void Bake(IBaker baker);
    }
}