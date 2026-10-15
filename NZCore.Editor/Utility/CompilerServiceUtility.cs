// <copyright project="NZCore" file="CompilerServiceUtility.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace NZCore
{
    public static class CompilerServiceUtility
    {
        private const string AdditionalFileExtension = ".additionalfile";
        private const string DataExtension = ".data";
        private const string SignatureSuffix = "_Signature";

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

            MigrateToSignature(fileName, sourceGeneratorAssemblyName);
            WriteSignature(tuple.resolvedPath);
        }

        public static void DeleteAdditionalFile(string fileName, string sourceGeneratorAssemblyName)
        {
            var dataPath = GetAdditionalFilePath(fileName, sourceGeneratorAssemblyName);

            DeleteFile(GetLegacyPath(fileName, sourceGeneratorAssemblyName));
            DeleteFile(dataPath);
            DeleteFile(GetSignaturePath(dataPath));
        }

        public static string GetAdditionalFilePath(string fileName, string sourceGeneratorAssemblyName) =>
            GetGeneratedFilePath($"{fileName}.{sourceGeneratorAssemblyName}{DataExtension}");

        internal static bool IsSignedDataFile(string assetPath)
        {
            var root = Editor.NZCoreProjectSettings.instance.GeneratedFilesRoot.TrimEnd('/') + "/";
            return assetPath.StartsWith(root, StringComparison.Ordinal) && TrySplitFileName(Path.GetFileName(assetPath), DataExtension, out _, out _);
        }

        internal static void WriteSignature(string dataPath)
        {
            var resolvedDataPath = Path.IsPathRooted(dataPath) ? dataPath : Path.Combine(GetProjectPath(), dataPath).Replace(Path.DirectorySeparatorChar, '/');
            var signaturePath = GetSignaturePath(resolvedDataPath);

            if (FileUtility.WriteChanges(signaturePath, Hash128.Compute(File.ReadAllText(resolvedDataPath)).ToString()))
            {
                AssetDatabase.ImportAsset(GetProjectRelativePath(signaturePath), ImportAssetOptions.ForceUpdate);
            }
        }

        // Earlier versions wrote the JSON as {name}.{generator}.additionalfile, which Unity passes to every assembly that inherits the generator.
        [InitializeOnLoadMethod]
        private static void MigrateGeneratedFiles()
        {
            EditorApplication.delayCall += () =>
            {
                var root = GetGeneratedFilePath(string.Empty);
                if (!Directory.Exists(root))
                {
                    return;
                }

                foreach (var path in Directory.GetFiles(root))
                {
                    var fileName = Path.GetFileName(path);

                    if ((TrySplitFileName(fileName, AdditionalFileExtension, out var name, out var generator) &&
                         !name.EndsWith(SignatureSuffix, StringComparison.Ordinal)) ||
                        TrySplitFileName(fileName, DataExtension, out name, out generator))
                    {
                        MigrateToSignature(name, generator);
                    }
                }
            };
        }

        // Moves {name}.{generator}.additionalfile to {name}.{generator}.data without regenerating it and makes sure its signature exists.
        private static void MigrateToSignature(string fileName, string sourceGeneratorAssemblyName)
        {
            var legacyPath = GetLegacyPath(fileName, sourceGeneratorAssemblyName);
            var dataPath = GetAdditionalFilePath(fileName, sourceGeneratorAssemblyName);

            if (File.Exists(legacyPath))
            {
                if (File.Exists(dataPath))
                {
                    DeleteFile(legacyPath);
                }
                else
                {
                    var error = AssetDatabase.MoveAsset(GetProjectRelativePath(legacyPath), GetProjectRelativePath(dataPath));
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogError($"Couldn't move {legacyPath} to {dataPath}: {error}");
                        return;
                    }
                }
            }

            if (File.Exists(dataPath) && !File.Exists(GetSignaturePath(dataPath)))
            {
                WriteSignature(dataPath);
            }
        }

        private static string GetLegacyPath(string fileName, string sourceGeneratorAssemblyName) =>
            GetGeneratedFilePath($"{fileName}.{sourceGeneratorAssemblyName}{AdditionalFileExtension}");

        private static string GetGeneratedFilePath(string fileName) =>
            Path.Combine(GetProjectPath(), Editor.NZCoreProjectSettings.instance.GeneratedFilesRoot, fileName).Replace(Path.DirectorySeparatorChar, '/');

        // {dir}/{name}.{generator}.data -> {dir}/{name}_Signature.{generator}.additionalfile
        // No '.' between name and Signature, Unity reads everything after the first one as the generator name.
        private static string GetSignaturePath(string dataPath)
        {
            var directory = Path.GetDirectoryName(dataPath)!.Replace(Path.DirectorySeparatorChar, '/');
            TrySplitFileName(Path.GetFileName(dataPath), DataExtension, out var name, out var generator);

            return $"{directory}/{name}{SignatureSuffix}.{generator}{AdditionalFileExtension}";
        }

        // {name}.{generator}{extension}, the name ends at the first '.'.
        private static bool TrySplitFileName(string fileName, string extension, out string name, out string generator)
        {
            var nameEnd = fileName.IndexOf('.');
            var generatorEnd = fileName.Length - extension.Length;

            if (!fileName.EndsWith(extension, StringComparison.Ordinal) || nameEnd < 0 || nameEnd >= generatorEnd)
            {
                name = generator = null;
                return false;
            }

            name = fileName.Substring(0, nameEnd);
            generator = fileName.Substring(nameEnd + 1, generatorEnd - nameEnd - 1);
            return true;
        }

        private static void DeleteFile(string resolvedPath)
        {
            if (AssetDatabase.DeleteAsset(GetProjectRelativePath(resolvedPath)))
            {
                return;
            }

            File.Delete(resolvedPath);
            File.Delete($"{resolvedPath}.meta");
        }

        private static (string resolvedPath, string content) GetAdditionalFileJson(object assets, string fileName, string sourceGeneratorAssemblyName,
            Formatting formatting)
        {
            var json = JsonConvert.SerializeObject(assets, formatting);
            return (GetAdditionalFilePath(fileName, sourceGeneratorAssemblyName), json);
        }

        private static string GetProjectRelativePath(string resolvedPath) =>
            resolvedPath.Substring(GetProjectPath().TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace(Path.DirectorySeparatorChar, '/');

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
