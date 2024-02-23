using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities.UI;

namespace NZCore.Editor
{
    public static class UIToolkitHelper
    {
        public static VisualElement GetHeaderField(string label)
        {
            var header = new Label(label);
            header.style.unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold);
            header.style.paddingTop = new StyleLength(10);
            header.style.paddingBottom = new StyleLength(10);

            return header;
        }
        
        /// <summary>
        /// Adds any kind of struct to the VisualElement root
        /// Used in the Entities inspector to show IComps and other data
        /// </summary>
        /// <param name="root"></param>
        /// <param name="data"></param>
        /// <typeparam name="T"></typeparam>
        public static void AddStructInspector<T>(this VisualElement root, T data)
            where T : struct
        {
            var content = new PropertyElement();
            root.Add(content);
            content.SetTarget(data);
        }
    }
}