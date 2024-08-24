// <copyright project="NZCore" file="CompilerServiceUtility.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NZCore.AssetManagement;
using NZCore.Utility;

namespace NZCore
{
    public static class CompilerServiceUtility
    {
        public static bool AddAdditionalFiles(string cscPath, params string[] additionalFiles)
        {
            List<string> lines = new List<string>();

            if (File.Exists(cscPath))
            {
                var cscContent = File.ReadAllLines(cscPath);
                lines.AddRange(cscContent);
                bool addedLines = false;

                foreach (var additionalFile in additionalFiles)
                {
                    var filename = Path.GetFileNameWithoutExtension(additionalFile);
                    bool found = false;
                    foreach (var line in cscContent)
                    {
                        //if (line.Contains($"{data.StructName}.default.json"))

                        if (line.Contains($"/{filename}")) // add / so Contains isn't confused with something like DynamicStat.cs and Stat.cs
                        {
                            found = true;
                        }
                    }

                    if (!found)
                    {
                        addedLines = true;
                        lines.Add($"/additionalfile:{additionalFile}");
                    }
                }

                if (addedLines)
                {
                    File.WriteAllLines(cscPath, lines);
                    return true;
                }

                return false;
            }

            foreach (var additionalFile in additionalFiles)
            {
                lines.Add($"/additionalfile:{additionalFile}");
            }

            File.WriteAllLines(cscPath, lines);
            return true;
        }

        public static bool CheckForJsonChanges(object assets, string structName, string packagePath)
        {
            var json = JsonConvert.SerializeObject(assets, Formatting.Indented);
            var csVersion = $"/*{json}*/";
            var path = $"Packages/{packagePath}";
            var jsonPath = $"{path}/{structName}.settings.cs";
            var resolvedJsonPath = Path.GetFullPath(jsonPath);

            return FileUtility.CheckForChanges(resolvedJsonPath, csVersion);
        }

        public static void WriteJson(object assets, string fileName, string packagePath, params string[] cscPaths)
        {
            var json = JsonConvert.SerializeObject(assets, Formatting.Indented);
            var csVersion = $"/*{json}*/";
            var path = $"Packages/{packagePath}";
            var jsonPath = $"{path}/{fileName}.settings.cs";
            var resolvedJsonPath = Path.GetFullPath(jsonPath);

            FileUtility.WriteChanges(resolvedJsonPath, csVersion);

            foreach (var cscPath in cscPaths)
            {
                var fullCscPath = Path.GetFullPath($"Packages/{cscPath}/csc.rsp");

                if (AddAdditionalFiles(fullCscPath, resolvedJsonPath))
                {
                    // trigger a compile ?
                }
            }
        }
    }
}