using System;

namespace NZCore.Editor.AssetManagement
{
    public class CscPathAttribute : Attribute
    {
        public string[] Path;

        public CscPathAttribute(params string[] path)
        {
            Path = path;
        }
    }
}