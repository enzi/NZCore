// <copyright project="NZCore.Editor" file="SettingsOverviewWindowSettings.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEditor;
using UnityEngine;

namespace NZCore.Editor
{
    [FilePath("ProjectSettings/NZCoreSetting.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class NZCoreProjectSettings : ScriptableSingleton<NZCoreProjectSettings>
    {
        [SerializeField] private string _settingsRoot = "Assets/Settings";

        public string SettingsRoot => _settingsRoot;

        public void SetSettingsRoot(string path)
        {
            _settingsRoot = path;
            Save(true);
        }
    }
}
