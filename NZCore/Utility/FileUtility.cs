// <copyright project="NZCore" file="FileUtility.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.IO;

namespace NZCore
{
    public static class FileUtility
    {
        public static bool WriteChanges(string filePath, string content)
        {
            var hasChanges = CheckForChanges(filePath, content);

            if (hasChanges)
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                File.WriteAllText(filePath, content);
            }

            return hasChanges;
        }

        public static bool CheckForChanges(string filePath, string content)
        {
            if (!File.Exists(filePath))
                return true;

            var oldData = File.ReadAllText(filePath);

            return oldData != content;
        }
    }
}