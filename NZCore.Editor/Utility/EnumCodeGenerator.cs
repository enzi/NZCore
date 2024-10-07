// <copyright project="NZCore.Editor" file="EnumCodeGenerator.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NZCore.AssetManagement;
using UnityEngine;

namespace NZCore.Editor
{
    public static class EnumCodeGenerator
    {
        public static string GetSourceCodeForEnum<T>(this List<T> allAssets, string enumName, Type enumType, bool flagEnum = false, string defaultNone = "None", string defaultNamespace = "NZCore")
            where T : ScriptableObject, IAutoID
        {
            BlockWriter bw = new BlockWriter();

            bw.AppendLine("using System;");
            bw.AppendLine();
            bw.AppendLine($"namespace {defaultNamespace}");
            bw.BeginBlock();

            bw.AppendLine($"[Serializable]");

            if (flagEnum)
                bw.AppendLine("[Flags]");

            bw.AppendLine($"public enum {enumName} : {enumType}");
            bw.BeginBlock();

            if (!flagEnum)
            {
                bw.AppendLine($"{defaultNone} = 0,");

                foreach (var def in allAssets)
                {
                    var trimmed = Regex.Replace(def.name, @" ", "");
                    bw.AppendLine($"{trimmed} = {def.AutoID},");
                }
            }
            else
            {
                bw.AppendLine($"{defaultNone} = 0,");

                foreach (var def in allAssets)
                {
                    var trimmed = Regex.Replace(def.name, @" ", "");
                    bw.AppendLine($"{trimmed} = 1 << {(def.AutoID - 1)},");
                }
            }

            bw.EndBlock();

            bw.EndBlock();

            return bw.StringBuilder.ToString();
        }
    }
}