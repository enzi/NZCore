using System.IO;

namespace NZCore.Utility
{
    public static class FileUtility
    {
        public static bool WriteChanges(string path, string content)
        {
            if (File.Exists(path))
            {
                var oldData = File.ReadAllText(path);

                if (oldData == content)
                {
                    return false;
                }
            }

            File.WriteAllText(path, content);
            return true;
        }
    }
}