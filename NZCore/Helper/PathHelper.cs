// <copyright project="NZCore" file="PathHelper.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System.IO;

namespace NZCore
{
    public static class PathHelper
    {
        public static string GetOrCreatePath(string mainPath, string path)
        {
            var fullPath = Path.GetFullPath(mainPath + path); // "Packages/com.nzspellcasting/NZCore.Stats/Runtime/CodeGenerated/Enums/DynamicStatTypes.cs");
            var dir = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(dir);

            return fullPath;
        }

        public static string GetOrCreateCodeGeneratedPath(string path)
        {
            return GetOrCreatePath("Assets/NZSpellCasting.CodeGenerated/", path);
        }
    }
}