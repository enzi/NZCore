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