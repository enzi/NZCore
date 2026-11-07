// <copyright project="NZCore.Authoring" file="AutoIDConflictHeader.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Linq;
using NZCore.AssetManagement;
using UnityEditor;
using UnityEngine;

namespace NZCore.Editor
{
    [InitializeOnLoad]
    internal static class AutoIDConflictHeader
    {
        static AutoIDConflictHeader()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnHeaderGUI;
        }

        private static void OnHeaderGUI(UnityEditor.Editor editor)
        {
            if (editor.targets.Length != 1 || editor.target is not ScriptableObjectWithAutoID asset || asset.ConflictMode != AutoIDConflictMode.Error ||
                asset.AutoID == 0 || !AutoIDConflicts.TryGetOthers(asset, out var others))
            {
                return;
            }

            EditorGUILayout.HelpBox($"{asset.name} shares AutoID {asset.AutoID} with {string.Join(", ", others.Select(other => other.name))}. " +
                                    "Each keeps it until all but one get a new ID.", MessageType.Error);

            if (GUILayout.Button("Assign new ID"))
            {
                AutoIDConflicts.AssignNewId(asset);
            }
        }
    }
}