// <copyright project="NZCore" file="FileUtility.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.IO;

namespace NZCore
{
    public static class FileUtility
    {
        public static bool WriteChanges(string path, string content)
        {
            var hasChanges = CheckForChanges(path, content);

            if (hasChanges)
            {
                File.WriteAllText(path, content);
            }

            return hasChanges;
        }

        public static bool CheckForChanges(string path, string content)
        {
            if (!File.Exists(path))
                return true;

            var oldData = File.ReadAllText(path);

            return oldData != content;
        }
    }
}