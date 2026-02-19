// <copyright project="NZCore" file="CompilerServiceUtility.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace NZCore
{
    public static class CompilerServiceUtility
    {
        private static readonly Type monoIOType =  Type.GetType("System.IO.MonoIO, mscorlib");
        private static readonly MethodInfo remapPathMethod = monoIOType.GetMethod("RemapPath", BindingFlags.Static | BindingFlags.Public);
        
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
                
                // test for same files that could been left from other projects
                {
                    for (int i = lines.Count - 1; i >= 0; i--)
                    {
                        var line = lines[i].Replace("/additionalfile:", "");
                        var filenameInFile = Path.GetFileNameWithoutExtension(line);

                        foreach (var additionalFile in additionalFiles)
                        {
                            var filename = Path.GetFileNameWithoutExtension(additionalFile);

                            if (filename == filenameInFile)
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
                    foreach (var line in lines)
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

        /// <summary>
        /// Serialize an object into a json comment inside a cs file
        /// Output path is Packages/packagePath/fileName 
        /// </summary>
        public static (string resolvedJsonPath, string csVersion) CSifyJson(object assets, string fileName, string packagePath)
        {
            try
            {
                var json = JsonConvert.SerializeObject(assets, Formatting.Indented);
                var csVersion = $"/*{json}*/";
                var projectFullPath = GetProjectPath();
                var packageFullPath = Path.GetFullPath($"Packages/{packagePath}");

                if (RemapPath(packageFullPath, out var remappedPackagePath))
                {
                    packageFullPath = remappedPackagePath;
                }

                string jsonPath;
                if (packageFullPath.Contains(projectFullPath))
                {
                    var relativePackagePath = packageFullPath.Replace($"{projectFullPath}{Path.DirectorySeparatorChar}", "");
                    jsonPath = $"{relativePackagePath}/{fileName}.settings.cs";
                }
                else
                {
                    // fall back to absolute paths
                    jsonPath = Path.GetFullPath($"Packages/{packagePath}/{fileName}.settings.cs");
                }

                return (jsonPath, csVersion);
            }
            catch(Exception e)
            {
                Debug.LogError($"CSifyJson - {e.Message}");
                return (null, null);
            }
        }

        public static bool CheckForJsonChanges(object assets, string fileName, string packagePath)
        {
            var tuple = CSifyJson(assets, fileName, packagePath);

            return FileUtility.CheckForChanges(tuple.resolvedJsonPath, tuple.csVersion);
        }

        public static void WriteJson(object assets, string fileName, string packagePath, params string[] cscPaths)
        {
            var tuple = CSifyJson(assets, fileName, packagePath);

            FileUtility.WriteChanges(tuple.resolvedJsonPath, tuple.csVersion);
            
            foreach (var cscPath in cscPaths)
            {
                var fullCscPath = Path.GetFullPath($"Packages/{cscPath}/csc.rsp");

                AddAdditionalFiles(fullCscPath, tuple.resolvedJsonPath);
            }
        }

        public static bool RemapPath(string path, out string newPath)
        {
            if (remapPathMethod == null)
            {
                throw new Exception("RemapPathMethod is null! Reflection failed to resolve MonoIO.RemapPath!");
            }

            var tmpPath = "";
            object[] parameters = { path, tmpPath };
            bool result = (bool)remapPathMethod.Invoke(null, parameters);
            newPath = (string)parameters[1];
            

            return result;
        }

        public static string GetProjectPath()
        {
            var args = Environment.GetCommandLineArgs();

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.Equals("-projectPath", StringComparison.InvariantCultureIgnoreCase))
                    return args[i + 1];
            }

            return Path.GetFullPath("Assets/..");
        }

        public static string GetUniqueSettingsPath()
        {
            var companyName = PlayerSettings.companyName.ToLowerInvariant().Trim().Replace(" ", "-");
            var projectName = PlayerSettings.productName.ToLowerInvariant().Trim().Replace(" ", "-");

            if (companyName.Length > 0 && projectName.Length > 0)
            {
                return $"Settings/{companyName}.{projectName}";
            }
            else
            {
                return $"Settings/{projectName}";
            }
        }
    }
}
