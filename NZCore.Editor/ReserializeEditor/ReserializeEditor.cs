// <copyright project="NZCore.Editor" file="ReserializeEditor.cs" version="1.0.0">
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

        [MenuItem("Tools/NZCore/Reserialize Assets")]
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