// <copyright project="NZCore" file="SettingsBase.cs" version="0.1">
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