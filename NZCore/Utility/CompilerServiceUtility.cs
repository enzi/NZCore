// <copyright project="NZCore" file="CompilerServiceUtility.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NZCore.Utility;
using UnityEngine;

namespace NZCore
{
    public static class CompilerServiceUtility
    {
        public static void AddAdditionalFiles(string cscPath, params string[] additionalFiles)
        {
            List<string> lines = new List<string>();
            var randomSignature = $"#{Guid.NewGuid()}";
            
            if (File.Exists(cscPath))
            {
                var cscContent = File.ReadAllLines(cscPath);
                lines.AddRange(cscContent);

                // find #generation date (for triggering sourcegen in unity)
                {
                    int genLineIndex = -1;
                    for (var i = 0; i < lines.Count; i++)
                    {
                        var line = lines[i];
                        if (!line.StartsWith("#"))
                        {
                            continue;
                        }

                        genLineIndex = i;
                        break;
                    }

                    if (genLineIndex == -1)
                    {
                        lines.Insert(0, randomSignature);
                    }
                    else
                    {
                        lines[genLineIndex] = randomSignature;
                    }
                }

                // test for deleted references
                {
                    for (int i = lines.Count - 1; i >= 0; i--)
                    {
                        var line = lines[i];
                        if (line.StartsWith("/additionalfile:"))
                        {
                            var path = line.Replace("/additionalfile:", "");

                            if (!File.Exists(path))
                            {
                                lines.RemoveAt(i);
                            }
                        }
                    }
                }

                foreach (var additionalFile in additionalFiles)
                {
                    var filename = Path.GetFileNameWithoutExtension(additionalFile);
                    bool found = false;
                    foreach (var line in cscContent)
                    {
                        //if (line.Contains($"{data.StructName}.default.json"))

                        if (line.Contains($"/{filename}.")) // add / so Contains isn't confused with something like DynamicStat.cs and Stat.cs
                        {
                            found = true;
                        }
                    }

                    if (!found)
                    {
                        lines.Add($"/additionalfile:{additionalFile}");
                    }
                }
            }
            else
            {
                lines.Add(randomSignature);
                foreach (var additionalFile in additionalFiles)
                {
                    lines.Add($"/additionalfile:{additionalFile}");
                }    
            }

            File.WriteAllLines(cscPath, lines);
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

                AddAdditionalFiles(fullCscPath, resolvedJsonPath);
            }
        }
    }
}
