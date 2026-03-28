// <copyright project="NZCore.Editor" file="ReserializeEditor.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NZCore.Editor
{
    public class ReserializeEditor : EditorWindow
    {
        private string _path = "Assets/";

        [MenuItem("Tools/NZCore/Reserialize Assets")]
        private static void Init()
        {
            var window = (ReserializeEditor)GetWindow(typeof(ReserializeEditor));
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Reserialize Assets", EditorStyles.boldLabel);
            _path = EditorGUILayout.TextField("Path", _path);

            if (GUILayout.Button("Reserialize"))
            {
                var paths = AssetDatabase.FindAssets("", new[] { _path })
                                         .Select(AssetDatabase.GUIDToAssetPath)
                                         .ToArray();

                AssetDatabase.ForceReserializeAssets(paths, ForceReserializeAssetsOptions.ReserializeAssets);

                Debug.Log("Reserializing done!");
            }
        }
    }
}