using System;

namespace NZCore.Editor.AssetManagement
{
    public class DefaultAutoIDPathAttribute : Attribute
    {
        public string Path;

        public DefaultAutoIDPathAttribute(string path)
        {
            Path = path;
        }
    }
}