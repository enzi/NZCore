using Unity.Entities;
using UnityEngine;

namespace NZCore
{
    public static class BakerExtensions
    {
        public static bool TryGetComponent<T>(this IBaker baker, out T comp)
            where T : Component
        {
            comp = baker.GetComponent<T>();
            return comp != null;
        }
    }
}