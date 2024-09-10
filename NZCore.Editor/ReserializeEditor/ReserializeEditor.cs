// <copyright project="NZCore" file="ReserializeEditor.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NZCore.Editor
{
    public class ReserializeEditor : EditorWindow
    {
        private string path = "Assets/";

        [MenuItem("Tools/Reserialize assets")]
        private static void Init()
        {
            ReserializeEditor window = (ReserializeEditor)GetWindow(typeof(ReserializeEditor));
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Reserialize Assets", EditorStyles.boldLabel);
            path = EditorGUILayout.TextField("Path", path);

            if (GUILayout.Button("Reserialize"))
            {
                var paths = AssetDatabase.FindAssets("", new[] { path })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .ToArray();

                AssetDatabase.ForceReserializeAssets(paths, ForceReserializeAssetsOptions.ReserializeAssets);

                Debug.Log("Reserializing done!");
            }
        }
    }
}