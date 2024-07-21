using Unity.Entities.UI;
using UnityEngine;
using UnityEngine.UIElements;

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

        public static VisualElement AlignLabel(this VisualElement root)
        {
            root.AddToClassList(BaseField<TextField>.alignedFieldUssClassName);
            return root;
        }

        public static unsafe ref T DrawPropertyDrawer<T>(this VisualElement root, ref byte* ptr)
            where T : unmanaged
        {
            ref T data = ref *(T*)ptr;
            root.AddBoldLabel($"{typeof(T).Name}");
            root.AddStructInspector(data);
            root.Add(new Label()); // just used as a break
            ptr += sizeof(T);
            return ref data;
        }

        public static void AddBoldLabel(this VisualElement root, string text)
        {
            var lbl = new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold)
                }
            };

            root.Add(lbl);
        }
    }
}