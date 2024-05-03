using Unity.Entities;

namespace NZCore
{
    public static class DynamicComponentTypeHandleExtensions
    {
        public static ref readonly TypeIndex GetTypeIndex(this ref DynamicComponentTypeHandle handle)
        {
            return ref handle.m_TypeIndex;
        }
    }
}