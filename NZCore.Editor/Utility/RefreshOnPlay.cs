// <copyright project="NZCore.Editor" file="RefreshOnPlay.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEditor;

namespace NZCore.Editor
{
    [InitializeOnLoad]
    public class RefreshOnPlay
    {
        static RefreshOnPlay()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                AssetDatabase.Refresh(ImportAssetOptions.Default);
            }
        }
    }
}