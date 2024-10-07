// <copyright project="NZCore.Authoring" file="SettingsBase.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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