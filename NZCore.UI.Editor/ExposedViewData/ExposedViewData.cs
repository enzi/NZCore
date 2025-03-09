// <copyright project="NZCore.UI.Editor" file="ExposedViewData.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Reflection;
using UnityEngine.UIElements;

namespace NZCore.UI.Editor.ExposedViewData
{
    public static class ExposedViewData
    {
        private static readonly Type t_VisualElement =  typeof(VisualElement);
        private static readonly MethodInfo m_GetOrCreateViewData = t_VisualElement.GetMethod("GetOrCreateViewData", BindingFlags.NonPublic | BindingFlags.Instance, null, 
            new[] { typeof(object), typeof(string) }, null);
        private static readonly MethodInfo m_SaveViewData = t_VisualElement.GetMethod("SaveViewData", BindingFlags.NonPublic | BindingFlags.Instance);

        public static T GetOrCreateViewData<T>(VisualElement ve, string key, T data = null)
            where T : class, new()
        {
            if (m_GetOrCreateViewData == null)
            {
                return null;
            }

            var genericMethod = m_GetOrCreateViewData.MakeGenericMethod(typeof(T));
            return (T) genericMethod.Invoke(ve, new object[] { data, key });
        }

        public static void SaveViewData(VisualElement ve)
        {
            if (m_SaveViewData != null)
            {
                m_SaveViewData.Invoke(ve, null);
            }
        }
    }
}