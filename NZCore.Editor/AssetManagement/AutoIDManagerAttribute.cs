using System;

namespace NZCore.AssetManagement
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class AutoIDManagerAttribute : Attribute
    {
        public string ManagerType;
        public string ContainerListProperty;

        public AutoIDManagerAttribute(string managerType, string containerListProperty)
        {
            ManagerType = managerType;
            ContainerListProperty = containerListProperty;
        }
    }
}