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
        private const string DefaultGeneratedFilesRoot = "Assets/NZGenerated";

        [SerializeField] private string _settingsRoot = "Assets/Settings";
        [SerializeField] private string _generatedFilesRoot = DefaultGeneratedFilesRoot;

        public string SettingsRoot => _settingsRoot;
        public string GeneratedFilesRoot => string.IsNullOrEmpty(_generatedFilesRoot)
            ? DefaultGeneratedFilesRoot
            : _generatedFilesRoot;

        public void SetSettingsRoot(string path)
        {
            _settingsRoot = path;
            Save(true);
        }

        public void SetGeneratedFilesRoot(string path)
        {
            _generatedFilesRoot = path;
            Save(true);
        }
    }
}
