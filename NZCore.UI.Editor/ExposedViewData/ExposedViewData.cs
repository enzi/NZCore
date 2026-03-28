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
        private static readonly Type VisualElementType = typeof(VisualElement);

        private static readonly MethodInfo GetOrCreateViewDataMethod = VisualElementType.GetMethod("GetOrCreateViewData",
            BindingFlags.NonPublic | BindingFlags.Instance, null,
            new[] { typeof(object), typeof(string) }, null);

        private static readonly MethodInfo SaveViewDataMethod = VisualElementType.GetMethod("SaveViewData", BindingFlags.NonPublic | BindingFlags.Instance);

        public static T GetOrCreateViewData<T>(VisualElement ve, string key, T data = null)
            where T : class, new()
        {
            if (GetOrCreateViewDataMethod == null)
            {
                return null;
            }

            var genericMethod = GetOrCreateViewDataMethod.MakeGenericMethod(typeof(T));
            return (T)genericMethod.Invoke(ve, new object[] { data, key });
        }

        public static void SaveViewData(VisualElement ve)
        {
            if (SaveViewDataMethod != null)
            {
                SaveViewDataMethod.Invoke(ve, null);
            }
        }
    }
}