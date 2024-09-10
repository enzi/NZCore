// <copyright project="NZCore" file="SettingsAuthoring.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace NZCore.Settings
{
    public class SettingsAuthoring : MonoBehaviour
    {
        [SerializeField] private SettingsBase[] settings = { };

        private class SettingsBaker : Baker<SettingsAuthoring>
        {
            public override void Bake(SettingsAuthoring authoring)
            {
                foreach (var setting in authoring.settings.Distinct())
                {
                    if (setting == null)
                    {
                        Debug.LogWarning($"Setting is not set on {authoring} in {authoring.gameObject.scene}");
                        continue;
                    }

                    DependsOn(setting);
                    setting.Bake(this);
                }
            }
        }
    }
}