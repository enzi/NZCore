// <copyright project="NZCore.Editor" file="AssemblyDefinitionValidator.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NZCore.Editor
{
    public class AssemblyDefinitionValidator
    {
        [Serializable]
        private class AssemblyDefinitionScheme
        {
            public string name;
            public string rootNamespace;
            public string[] references;
            public string[] includePlatforms;
            public string[] excludePlatforms;
            public bool allowUnsafeCode;
            public bool overrideReferences;
            public string[] precompiledReferences;
            public bool autoReferenced;
            public string[] defineConstraints;
            public string[] versionDefines;
            public bool noEngineReferences;
        }

        private static readonly HashSet<string> AsmdefFilter = new() { ".Authoring" };

        [MenuItem("Tools/NZCore/Validate Assembly Definitions")]
        public static void ValidateAssemblyDefinitions()
        {
            var assets = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");

            int missingCount = 0;
            foreach (var guid in assets)
            {
                var filePath = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = Path.GetFileNameWithoutExtension(filePath);

                if (!AsmdefFilter.Any(identifier => fileName.Contains(identifier)))
                {
                    continue;
                }

                var json = File.ReadAllText(filePath);
                var asmdef = JsonUtility.FromJson<AssemblyDefinitionScheme>(json);

                if (asmdef.defineConstraints.Length == 0)
                {
                    missingCount++;
                    Debug.LogError($"Authoring asmdef has no constraint: {filePath}");
                }
            }

            if (missingCount == 0)
                Debug.Log("AssemblyDefinitionValidator found no problems!");
        }
    }
}