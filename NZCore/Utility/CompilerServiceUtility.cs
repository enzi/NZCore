using System.Collections.Generic;
using System.IO;

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

                        if (line.Contains(filename))
                        {
                            found = true;
                        }
                    }

                    if (!found)
                    {
                        addedLines = true;
                        //cscContent.Add($"/additionalfile:{resolvedJsonPath}");
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
            else
            {
                foreach (var additionalFile in additionalFiles)
                {
                    lines.Add($"/additionalfile:{additionalFile}");
                }

                File.WriteAllLines(cscPath, lines);
                return true;
            }
        }
    }
}