// <copyright project="NZCore" file="CompilerServiceUtility.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;

namespace NZCore
{
    public static class CompilerServiceUtility
    {
        public static bool CheckForAdditionalFileJsonChanges(object assets, string fileName, string sourceGeneratorAssemblyName,
            Formatting formatting = Formatting.Indented)
        {
            var tuple = GetAdditionalFileJson(assets, fileName, sourceGeneratorAssemblyName, formatting);

            return FileUtility.CheckForChanges(tuple.resolvedPath, tuple.content);
        }

        public static void WriteAdditionalFileJson(object assets, string fileName, string sourceGeneratorAssemblyName,
            Formatting formatting = Formatting.Indented)
        {
            var tuple = GetAdditionalFileJson(assets, fileName, sourceGeneratorAssemblyName, formatting);
            if (FileUtility.WriteChanges(tuple.resolvedPath, tuple.content))
            {
                AssetDatabase.ImportAsset(GetProjectRelativePath(tuple.resolvedPath), ImportAssetOptions.ForceUpdate);
            }
        }

        public static void DeleteAdditionalFile(string fileName, string sourceGeneratorAssemblyName)
        {
            var resolvedPath = GetAdditionalFilePath(fileName, sourceGeneratorAssemblyName);
            var assetPath = GetProjectRelativePath(resolvedPath);

            if (AssetDatabase.DeleteAsset(assetPath))
            {
                return;
            }

            File.Delete(resolvedPath);
            File.Delete($"{resolvedPath}.meta");
        }

        public static string GetAdditionalFilePath(string fileName, string sourceGeneratorAssemblyName) =>
            Path.Combine(GetProjectPath(), Editor.NZCoreProjectSettings.instance.GeneratedFilesRoot,
                $"{fileName}.{sourceGeneratorAssemblyName}.additionalfile").Replace(Path.DirectorySeparatorChar, '/');

        private static (string resolvedPath, string content) GetAdditionalFileJson(object assets, string fileName, string sourceGeneratorAssemblyName,
            Formatting formatting)
        {
            var json = JsonConvert.SerializeObject(assets, formatting);
            return (GetAdditionalFilePath(fileName, sourceGeneratorAssemblyName), json);
        }

        private static string GetProjectRelativePath(string resolvedPath) => resolvedPath
                                                                             .Substring(GetProjectPath().TrimEnd(Path.DirectorySeparatorChar).Length + 1)
                                                                             .Replace(Path.DirectorySeparatorChar, '/');

        public static string GetProjectPath()
        {
            var args = Environment.GetCommandLineArgs();

            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("-projectPath", StringComparison.InvariantCultureIgnoreCase))
                {
                    return Path.GetFullPath(args[i + 1]);
                }
            }

            return Path.GetFullPath("Assets/..");
        }
    }
}