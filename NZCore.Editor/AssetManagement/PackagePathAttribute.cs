using System;

namespace NZCore.Editor.AssetManagement
{
    public class PackagePathAttribute : Attribute
    {
        public string Path;

        public PackagePathAttribute(string path)
        {
            Path = path;
        }
    }
}